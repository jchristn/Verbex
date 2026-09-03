@echo off
REM Connect Codex to the Verbex MCP server by adding a 'verbex' entry to %USERPROFILE%\.codex\config.json.
REM The Verbex MCP server requires no authentication.
REM Usage: install-codex.bat
REM Override with VERBEX_MCP_URL / VERBEX_CODEX_CONFIG.
setlocal
if "%VERBEX_MCP_URL%"=="" set "VERBEX_MCP_URL=http://127.0.0.1:8200/mcp"
if "%VERBEX_CODEX_CONFIG%"=="" set "VERBEX_CODEX_CONFIG=%USERPROFILE%\.codex\config.json"
set "VERBEX_CONFIG=%VERBEX_CODEX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:VERBEX_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $raw=''; if(Test-Path $p){ $raw=Get-Content -Raw -Path $p }; if([string]::IsNullOrWhiteSpace($raw)){ $root=[PSCustomObject]@{} } else { $root=$raw | ConvertFrom-Json }; if($null -eq $root.mcpServers){ $root | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([PSCustomObject]@{}) -Force }; $entry=[PSCustomObject]@{ type='http'; url=$env:VERBEX_MCP_URL }; $root.mcpServers | Add-Member -NotePropertyName verbex -NotePropertyValue $entry -Force; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Added verbex to ' + $p)"
echo Restart Codex to pick up the change.
endlocal
