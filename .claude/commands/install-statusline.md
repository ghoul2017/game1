安装 Token 状态栏插件。在终端底部始终显示模型名称、Token 用量、上下文窗口进度条，以及智谱套餐用量、DeepSeek 费用或通用费用（自动判断）。

执行以下步骤：

1. 检查 `node` 是否可用（`node --version`）。不可用则报错停止
2. 在 `.claude/` 目录下创建 `statusline.js`，内容如下（原样写入，不要修改）：

```javascript
let input = '';
process.stdin.on('data', (chunk) => { input += chunk; });
process.stdin.on('end', () => {
    try {
        var data = JSON.parse(input);
    } catch (e) {
        console.log('loading...');
        return;
    }

    var parts = [];
    var fs = require('fs');
    var path = require('path');

    // ANSI 颜色
    var C = {
        reset: '\x1b[0m',
        bold: '\x1b[1m',
        dim: '\x1b[2m',
        cyan: '\x1b[36m',
        green: '\x1b[32m',
        yellow: '\x1b[33m',
        red: '\x1b[31m',
        magenta: '\x1b[35m',
        blue: '\x1b[34m',
        white: '\x1b[37m',
    };

    // ── 模型名称 ──
    var model = (data.model && data.model.display_name) || '';
    if (model) {
        parts.push(C.cyan + C.bold + model + C.reset);
    }

    // ── Token 用量（当前请求） ──
    var cw = data.context_window || {};
    var usage = (cw.current_usage) || {};
    var inp = usage.input_tokens || 0;
    var out = usage.output_tokens || 0;
    var cacheRead = usage.cache_read_input_tokens || 0;
    var cacheWrite = usage.cache_creation_input_tokens || 0;
    if (inp || out) {
        var t = C.green + 'in:' + fmtNum(inp) + C.reset;
        t += ' ' + C.yellow + 'out:' + fmtNum(out) + C.reset;
        if (cacheRead || cacheWrite) {
            t += ' ' + C.dim + 'cache:' + fmtNum(cacheRead) + '/' + fmtNum(cacheWrite) + C.reset;
        }
        parts.push(t);
    }

    // ── 上下文窗口 + 进度条 ──
    var totalIn = cw.total_input_tokens || 0;
    var totalOut = cw.total_output_tokens || 0;
    var limit = cw.context_window_size || 0;
    var usedPct = cw.used_percentage;
    if (totalIn && limit) {
        var total = totalIn + totalOut;
        var pct = usedPct != null ? usedPct : Math.round(total * 100 / limit);
        var ts = total >= 1000 ? (total / 1000).toFixed(1) + 'k' : String(total);
        var ls = limit >= 1000 ? (limit / 1000).toFixed(0) + 'k' : String(limit);

        var pctColor = pct < 50 ? C.green : (pct < 80 ? C.yellow : C.red);
        var bar = makeBar(pct, 12, pctColor);
        parts.push(bar + ' ' + pctColor + pct + '%' + C.dim + ' (' + ts + '/' + ls + ')' + C.reset);
    }

    // ── 费用 / 智谱套餐 / DeepSeek 实价（三选一） ──
    var baseUrl = process.env.ANTHROPIC_BASE_URL || '';
    var isZhiPu = baseUrl.indexOf('bigmodel') !== -1 || baseUrl.indexOf('z.ai') !== -1;
    var isDeepSeek = baseUrl.indexOf('deepseek') !== -1;

    if (isZhiPu) {
        var planInfo = getPlanUsage(fs, path);
        if (planInfo) {
            parts.push(planInfo);
        }
    } else if (isDeepSeek) {
        var deepseekCost = getDeepSeekCost(fs, path, data);
        if (deepseekCost) {
            parts.push(deepseekCost);
        }
    } else {
        var cost = (data.cost && data.cost.total_cost_usd) || 0;
        if (cost) {
            parts.push(C.magenta + '$' + cost.toFixed(4) + C.reset);
        }
    }

    console.log(parts.length ? parts.join(C.dim + ' | ' + C.reset) : 'ready');

    function fmtNum(n) {
        if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
        if (n >= 1000) return (n / 1000).toFixed(1) + 'k';
        return String(n);
    }

    function makeBar(pct, width, color) {
        var filled = Math.round(pct / 100 * width);
        var empty = width - filled;
        return color + '█'.repeat(filled) + C.dim + '░'.repeat(empty) + C.reset;
    }

    function fmtCountdown(ms) {
        if (ms <= 0) return '-';
        var mins = Math.floor(ms / 60000);
        var hrs = Math.floor(mins / 60);
        var days = Math.floor(hrs / 24);
        mins = mins % 60;
        hrs = hrs % 24;
        if (days > 0) return days + 'd' + hrs + 'h';
        if (hrs > 0) return hrs + 'h' + mins + 'm';
        return mins + 'm';
    }

    function getDeepSeekCost(fs, path, data) {
        var cacheFile = path.join(path.resolve(__dirname), 'statusline-deepseek-cache.json');
        var cache = null;
        try {
            var raw = fs.readFileSync(cacheFile, 'utf8');
            cache = JSON.parse(raw);
        } catch (e) { /* 首次运行 */ }

        var cw = data.context_window || {};
        var totalIn = cw.total_input_tokens || 0;
        var totalOut = cw.total_output_tokens || 0;
        var usage = cw.current_usage || {};
        var inp = usage.input_tokens || 0;
        var out = usage.output_tokens || 0;
        var cacheRead = usage.cache_read_input_tokens || 0;
        var cacheCreate = usage.cache_creation_input_tokens || 0;

        // 新会话检测：合计值下降说明是新会话
        if (cache && (totalIn + totalOut < cache.lastTotal)) {
            cache = null;
        }

        // 去重：同一轮次的 usage 指纹不变
        var usageFp = inp + ',' + out + ',' + cacheRead + ',' + cacheCreate;

        if (!cache || cache.usageFp !== usageFp) {
            if (!cache) {
                cache = { accIn: 0, accOut: 0, accCacheRead: 0, accCacheCreate: 0, lastTotal: 0, usageFp: '' };
            }
            cache.accIn += inp;
            cache.accOut += out;
            cache.accCacheRead += cacheRead;
            cache.accCacheCreate += cacheCreate;
            cache.lastTotal = totalIn + totalOut;
            cache.usageFp = usageFp;

            try {
                fs.writeFileSync(cacheFile, JSON.stringify(cache));
            } catch (e) { /* ignore */ }
        }

        // DeepSeek V4-Pro 2.5折定价（¥/百万token）
        var costIn = cache.accIn * 3.0 / 1000000;
        var costCacheRead = cache.accCacheRead * 0.025 / 1000000;
        var costCacheCreate = cache.accCacheCreate * 3.0 / 1000000;
        var costOut = cache.accOut * 6.0 / 1000000;
        var totalCost = costIn + costCacheRead + costCacheCreate + costOut;

        if (totalCost < 0.005) return null;

        return C.magenta + '¥' + totalCost.toFixed(4) + C.reset;
    }

    function getPlanUsage(fs, path) {
        var apiKey = process.env.ANTHROPIC_AUTH_TOKEN;
        if (!apiKey) return null;

        var cacheDir = path.resolve(__dirname);
        var cacheFile = path.join(cacheDir, 'statusline-plan-cache.json');

        var now = Date.now();
        var cacheData = null;
        try {
            var raw = fs.readFileSync(cacheFile, 'utf8');
            cacheData = JSON.parse(raw);
            if (cacheData && cacheData.timestamp && (now - cacheData.timestamp < 300000)) {
                return formatPlanInfo(cacheData.data);
            }
        } catch (e) { /* 缓存不存在或过期 */ }

        // 异步刷新缓存
        try {
            var https = require('https');
            var urlObj = new URL('https://open.bigmodel.cn/api/monitor/usage/quota/limit');
            var req = https.request({
                hostname: urlObj.hostname,
                path: urlObj.pathname,
                method: 'GET',
                headers: {
                    'Authorization': apiKey,
                    'Accept-Language': 'zh-CN',
                    'Content-Type': 'application/json',
                },
                timeout: 5000,
            }, function (res) {
                var body = '';
                res.on('data', function (chunk) { body += chunk; });
                res.on('end', function () {
                    try {
                        var apiData = JSON.parse(body);
                        if (apiData && apiData.success) {
                            fs.writeFileSync(cacheFile, JSON.stringify({
                                timestamp: Date.now(),
                                data: apiData,
                            }));
                        }
                    } catch (e) { /* ignore */ }
                });
            });
            req.on('error', function () { /* ignore */ });
            req.on('timeout', function () { req.destroy(); });
            req.end();
        } catch (e) { /* ignore */ }

        if (cacheData && cacheData.data) {
            return formatPlanInfo(cacheData.data);
        }
        return null;
    }

    function formatPlanInfo(apiData) {
        if (!apiData || !apiData.data || !apiData.data.limits) return null;

        var limits = apiData.data.limits;
        var level = apiData.data.level || '';
        var nowMs = Date.now();

        // 收集所有 TOKENS_LIMIT
        var tokenLimits = [];
        for (var i = 0; i < limits.length; i++) {
            if (limits[i].type === 'TOKENS_LIMIT') {
                tokenLimits.push(limits[i]);
            }
        }
        if (tokenLimits.length === 0 && !level) return null;

        // 按重置时间排序（短→长）
        tokenLimits.sort(function (a, b) {
            return (a.nextResetTime || 0) - (b.nextResetTime || 0);
        });

        // 固定三个周期槽位，按重置时间距离映射
        var slots = [
            { label: '5h', pct: 0, reset: null },
            { label: '1w', pct: 0, reset: null },
            { label: '1m', pct: 0, reset: null },
        ];

        for (var j = 0; j < tokenLimits.length; j++) {
            var lim = tokenLimits[j];
            var unit = (lim.unit || 0);
            var idx;
            if (unit === 3) idx = 0;       // < 6h → 5h
            else if (unit === 6) idx = 1;  // < 10d → 1w
            else idx = 2;                                // ≥ 10d → 1m
            slots[idx].pct = lim.percentage || 0;
            slots[idx].reset = lim.nextResetTime || 0;
        }

        // 构建显示
        var planParts = [];
        if (level) {
            planParts.push(C.blue + C.bold + level.toUpperCase() + C.reset);
        }

        for (var k = 0; k < slots.length; k++) {
            var s = slots[k];
            var pctColor = s.pct < 50 ? C.green : (s.pct < 80 ? C.yellow : C.red);
            var countdown = s.reset ? fmtCountdown(s.reset - nowMs) : '-';
            planParts.push(
                C.dim + s.label + '/' + C.reset +
                C.dim + countdown + ':' + C.reset +
                pctColor + s.pct + '%' + C.reset
            );
        }

        return planParts.join(' ');
    }
});
```

3. 读取项目 `.claude/settings.json`，确认是否已有 `statusLine` 配置。如没有，在顶层添加：

```json
"statusLine": {
    "type": "command",
    "command": "node .claude/statusline.js"
}
```

4. 验证：用 echo 测试脚本输出是否正常
5. 输出安装结果：告知用户状态栏已生效（自动生效，无需重启），以及会显示什么内容
