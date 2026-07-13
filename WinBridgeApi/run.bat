@echo off
REM Runs WinBridgeApi in the foreground, bound to plain HTTP on loopback only.
REM Program.cs already pins Kestrel to localhost:5000; --urls is kept
REM here as an explicit safety net / documentation of the intended address.

cd /d "%~dp0"
dotnet run --urls "http://127.0.0.1:5000"
