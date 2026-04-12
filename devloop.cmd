@echo off
REM devloop.cmd - Unattended auto dev loop
REM Usage: devloop.cmd [max_loops]
REM Example: devloop.cmd 5

setlocal

set MAX_LOOPS=%1
if "%MAX_LOOPS%"=="" set MAX_LOOPS=8

set LOOP_COUNT=0

:LOOP
set /a LOOP_COUNT+=1
echo ========================================
echo [DevLoop] Round %LOOP_COUNT%/%MAX_LOOPS%
echo ========================================

call claude -p "Run /dev" --dangerously-skip-permissions

if %ERRORLEVEL% NEQ 0 (
    echo [DevLoop] Round %LOOP_COUNT% failed, stopping
    goto END
)

if %LOOP_COUNT% GEQ %MAX_LOOPS% (
    echo [DevLoop] Max loops %MAX_LOOPS% reached, stopping
    goto END
)

goto LOOP

:END
echo ========================================
echo [DevLoop] Done. Total rounds: %LOOP_COUNT%
echo ========================================
endlocal
