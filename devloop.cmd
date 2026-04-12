@echo off
REM devloop.cmd - 无人值守自动开发循环
REM 用法: devloop.cmd [最大循环次数]
REM 示例: devloop.cmd 5

setlocal

set MAX_LOOPS=%1
if "%MAX_LOOPS%"=="" set MAX_LOOPS=8

set LOOP_COUNT=0

:LOOP
set /a LOOP_COUNT+=1
echo ========================================
echo [DevLoop] 第 %LOOP_COUNT%/%MAX_LOOPS% 轮
echo ========================================

claude -p "运行 /devloop" --allowedTools "Read,Edit,Write,Glob,Grep,Agent,Bash(git:*),Bash(dotnet:*),Bash(ls:*),Bash(mkdir:*),Bash(cat:*),Bash(find:*),Bash(pwd:*),Bash(cd:*),Bash(cmd.exe:*)"

if %ERRORLEVEL% NEQ 0 (
    echo [DevLoop] 第 %LOOP_COUNT% 轮执行出错，停止循环
    goto END
)

if %LOOP_COUNT% GEQ %MAX_LOOPS% (
    echo [DevLoop] 已达到最大循环次数 %MAX_LOOPS%，停止
    goto END
)

goto LOOP

:END
echo ========================================
echo [DevLoop] 完成。共执行 %LOOP_COUNT% 轮
echo ========================================
endlocal
