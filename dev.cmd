@echo off
REM dev.cmd - 执行一次自动化开发（单个功能）
REM 用法: dev.cmd

echo [Dev] 执行单次自动化开发...
claude -p "运行 /dev" --dangerously-skip-permissions
echo [Dev] 完成
