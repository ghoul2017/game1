# 文档工作流知识

LINEKB:DOCS:AGENTS path=AGENTS.md note="game1 唯一活跃规则入口，规则用于保证安全、方向和连续性，不作为每轮固定流程脚本。"
LINEKB:DOCS:LINE_KB path=docs/line-kb note="行式知识库入口，用于 rg 检索结构、方向、路径、规则和易错点。"
LINEKB:DOCS:MODULES path=docs/模块文档 note="模块文档目录，查模块行为时先读模块文档再到代码确认。"
LINEKB:DOCS:ISSUES path=docs/遗留问题.md note="当前遗留问题入口。"
LINEKB:DOCS:ROBOT_MANUAL path=docs/Robot使用手册.md note="Robot 人类使用手册，修改 robot/ 时需按需读取。"
LINEKB:DOCS:TEST_MANUAL path=docs/test-manual.md note="测试相关手册入口。"
LINEKB:DOCS:DESIGN_MODE doc=AGENTS.md note="用户说设计、先设计、系统设计时，默认只做完整设计和文档整理，不写代码、不改资源、不改配置。"
LINEKB:DOCS:IMPLEMENT_MODE doc=AGENTS.md note="正式实现先读影响范围内代码，按现有架构做完整一致的局部最优改动，并覆盖入口、调用方、测试、文档和知识库。"
LINEKB:DOCS:COMPLETE_STOP doc=AGENTS.md note="目标完成、必要文档更新和验证通过后停止，不在同一轮主动延伸新需求。"
