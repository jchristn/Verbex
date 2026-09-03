@echo off
REM Disconnect the Gemini CLI from Verbex by removing the 'verbex' entry from %USERPROFILE%\.gemini\settings.json.
setlocal
if "%VERBEX_GEMINI_CONFIG%"=="" set "VERBEX_GEMINI_CONFIG=%USERPROFILE%\.gemini\settings.json"
set "VERBEX_CONFIG=%VERBEX_GEMINI_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:VERBEX_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.mcpServers){ $root.mcpServers.PSObject.Properties.Remove('verbex') }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed verbex from ' + $p)"
endlocal
