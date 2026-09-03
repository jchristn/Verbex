@echo off
REM Disconnect Mux from Verbex by removing the 'verbex' entry from Mux's mcp-servers.json.
setlocal
if "%VERBEX_MUX_CONFIG%"=="" set "VERBEX_MUX_CONFIG=%USERPROFILE%\.mux\mcp-servers.json"
set "VERBEX_CONFIG=%VERBEX_MUX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:VERBEX_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.servers){ $root.servers=@($root.servers | Where-Object { $_.name -ne 'verbex' }) }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed verbex from ' + $p)"
endlocal
