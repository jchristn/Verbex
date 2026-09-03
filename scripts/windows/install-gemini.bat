@echo off
REM Connect the Gemini CLI to Verbex by adding a 'verbex' entry to %USERPROFILE%\.gemini\settings.json.
REM Gemini addresses streamable-HTTP servers with the "httpUrl" field.
REM The Verbex MCP server requires no authentication.
REM Usage: install-gemini.bat
REM Override with VERBEX_MCP_URL / VERBEX_GEMINI_CONFIG.
setlocal
if "%VERBEX_MCP_URL%"=="" set "VERBEX_MCP_URL=http://127.0.0.1:8200/mcp"
if "%VERBEX_GEMINI_CONFIG%"=="" set "VERBEX_GEMINI_CONFIG=%USERPROFILE%\.gemini\settings.json"
set "VERBEX_CONFIG=%VERBEX_GEMINI_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:VERBEX_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $raw=''; if(Test-Path $p){ $raw=Get-Content -Raw -Path $p }; if([string]::IsNullOrWhiteSpace($raw)){ $root=[PSCustomObject]@{} } else { $root=$raw | ConvertFrom-Json }; if($null -eq $root.mcpServers){ $root | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([PSCustomObject]@{}) -Force }; $entry=[PSCustomObject]@{ httpUrl=$env:VERBEX_MCP_URL }; $root.mcpServers | Add-Member -NotePropertyName verbex -NotePropertyValue $entry -Force; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Added verbex to ' + $p)"
echo Restart the Gemini CLI to pick up the change (run /mcp to verify).
endlocal
