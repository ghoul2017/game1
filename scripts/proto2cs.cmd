@echo off
chcp 65001 >nul
dotnet run --project "%~dp0..\tools" -- proto2cs %*
