@echo off
REM Disconnect Codex from Verbex by removing the 'verbex' entry from %USERPROFILE%\.codex\config.json.
setlocal
if "%VERBEX_CODEX_CONFIG%"=="" set "VERBEX_CODEX_CONFIG=%USERPROFILE%\.codex\config.json"
set "VERBEX_CONFIG=%VERBEX_CODEX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:VERBEX_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.mcpServers){ $root.mcpServers.PSObject.Properties.Remove('verbex') }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed verbex from ' + $p)"
endlocal
