# 编码规则（必须遵守）

- 文件编码：UTF-8 无 BOM，行尾 **CRLF**（Windows 下 git core.autocrlf=true 使本地文件为 CRLF；写 LF 会造成 diff 全篇飘红）
- .cmd 脚本中调用其他 .cmd **必须**使用 `call` 关键字（否则控制权不会返回）
