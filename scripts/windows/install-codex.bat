@echo off
REM Connect Codex to the Verbex MCP server by writing an [mcp_servers.verbex] table to %USERPROFILE%\.codex\config.toml.
REM The Verbex MCP server requires no authentication.
REM Usage: install-codex.bat
REM Override with VERBEX_MCP_URL / VERBEX_CODEX_CONFIG.
setlocal
if "%VERBEX_MCP_URL%"=="" set "VERBEX_MCP_URL=http://127.0.0.1:8200/mcp"
if "%VERBEX_CODEX_CONFIG%"=="" set "VERBEX_CODEX_CONFIG=%USERPROFILE%\.codex\config.toml"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:VERBEX_CODEX_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $lines=@(); if(Test-Path $p){ $lines=@(Get-Content -Path $p) }; $out=New-Object System.Collections.Generic.List[string]; $skip=$false; $found=$false; foreach($l in $lines){ if($l -match '^\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$'){ $n=$Matches[1].Replace([string][char]34,''); $skip=($n -eq 'mcp_servers.verbex' -or $n.StartsWith('mcp_servers.verbex.')); if($skip){ $found=$true } }; if(-not $skip){ $out.Add($l) } }; while($out.Count -gt 0 -and [string]::IsNullOrWhiteSpace($out[$out.Count-1])){ $out.RemoveAt($out.Count-1) }; if($out.Count -gt 0){ $out.Add('') }; $out.Add('[mcp_servers.verbex]'); $out.Add('url = ' + (ConvertTo-Json $env:VERBEX_MCP_URL)); [IO.File]::WriteAllText($p, (($out -join [Environment]::NewLine) + [Environment]::NewLine)); Write-Host ('Added verbex to ' + $p)"
if errorlevel 1 exit /b 1
echo Restart Codex to pick up the change.
endlocal
