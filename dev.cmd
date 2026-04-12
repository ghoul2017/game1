@echo off
REM dev.cmd - 执行一次自动化开发（单个功能）
REM 用法: dev.cmd

echo [Dev] 执行单次自动化开发...
claude -p "运行 /dev" --allowedTools "Read,Edit,Write,Glob,Grep,Agent,Bash(git:*),Bash(dotnet:*),Bash(ls:*),Bash(mkdir:*),Bash(cat:*),Bash(find:*),Bash(pwd:*),Bash(cd:*),Bash(cmd.exe:*)"
echo [Dev] 完成
