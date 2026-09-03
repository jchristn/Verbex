@echo off
REM Connect Mux to the Verbex MCP server by adding a 'verbex' entry to Mux's mcp-servers.json.
REM The Verbex MCP server requires no authentication.
REM Usage: install-mux.bat
REM Override the endpoint with VERBEX_MCP_BASE_URL and the config path with VERBEX_MUX_CONFIG.
setlocal
if "%VERBEX_MCP_BASE_URL%"=="" set "VERBEX_MCP_BASE_URL=http://127.0.0.1:8200"
if "%VERBEX_MUX_CONFIG%"=="" set "VERBEX_MUX_CONFIG=%USERPROFILE%\.mux\mcp-servers.json"
set "VERBEX_CONFIG=%VERBEX_MUX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:VERBEX_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $raw=''; if(Test-Path $p){ $raw=Get-Content -Raw -Path $p }; if([string]::IsNullOrWhiteSpace($raw)){ $root=[PSCustomObject]@{} } else { $root=$raw | ConvertFrom-Json }; if($null -eq $root.servers){ $root | Add-Member -NotePropertyName servers -NotePropertyValue @() -Force }; $others=@($root.servers | Where-Object { $_.name -ne 'verbex' }); $entry=[PSCustomObject]@{ name='verbex'; transport='http'; url=$env:VERBEX_MCP_BASE_URL; mcpPath='/mcp' }; $root.servers=@($others + $entry); [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Added verbex to ' + $p)"
echo Point Mux at this file with --mcp-config, or add it to your Mux config directory, then restart Mux.
endlocal
