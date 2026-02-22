@echo off
echo Starting Widget API Server...
start "Widget API" /MIN "Output\Server\WidgetApi.exe"

echo Waiting for server to start...
timeout /t 3 /nobreak >nul

echo Starting Desktop Widget...
start "" "Output\Widget\WpfApp1.exe"

exit
