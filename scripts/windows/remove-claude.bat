@echo off
REM Disconnect Claude Code from the Verbex MCP server (user scope), via the claude CLI.
REM Succeeds with "nothing to remove" when there is no 'verbex' entry; every other server is kept.
REM For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
REM Usage: scripts\windows\remove-claude.bat
REM Requires the claude CLI and Windows PowerShell.
setlocal
if /I "%~1"=="-h" goto help
if /I "%~1"=="--help" goto help
if "%~1"=="/?" goto help
REM The arguments go to PowerShell through the environment and are checked there; batch never echoes them.
set "MCPREG_SELF=%~f0"
set "MCPREG_ARG1=%~1"
set "MCPREG_ARG2=%~2"
set "MCPREG_ARG3=%~3"
set "MCPREG_ARGC=0"
:mcpreg_count
if [%1]==[] goto mcpreg_counted
set /a MCPREG_ARGC+=1
shift
goto mcpreg_count
:mcpreg_counted
where powershell >nul 2>nul
if errorlevel 1 (
    echo Error: powershell was not found on PATH. 1>&2
    endlocal & exit /b 1
)
REM The rest of the work is PowerShell, at the end of this file; cmd never reads past exit /b.
powershell -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; $f=[IO.File]::ReadAllText($env:MCPREG_SELF); $i=$f.IndexOf([string][char]10+'##'+' POWERSHELL'); if ($i -lt 0) { throw 'PowerShell section not found' }; $global:McpRegRc=1; try { Invoke-Expression $f.Substring($i); $global:McpRegRc=0 } catch { [Console]::Error.WriteLine('Error: ' + $_.Exception.Message) }; exit $global:McpRegRc"
set "MCPREG_RC=%errorlevel%"
endlocal & exit /b %MCPREG_RC%

:help
echo Disconnect Claude Code from the Verbex MCP server (user scope), via the claude CLI.
echo Succeeds with "nothing to remove" when there is no 'verbex' entry; every other server is kept.
echo For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
echo Usage: scripts\windows\remove-claude.bat
echo Requires the claude CLI and Windows PowerShell.
endlocal & exit /b 0

## POWERSHELL (run by the powershell line above; everything below is PowerShell, not batch)
$ErrorActionPreference = 'Stop'
$argc = [int]$env:MCPREG_ARGC
$arg1 = [string]$env:MCPREG_ARG1
$arg2 = [string]$env:MCPREG_ARG2
$arg3 = [string]$env:MCPREG_ARG3
# Windows PowerShell 5.1 serializes some arrays as {"value":[...],"Count":n} unless this type data is removed.
try { Remove-TypeData System.Array -ErrorAction SilentlyContinue } catch { }
function Get-EnvOr([string]$Name, [string]$Default) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrEmpty($value)) { return $Default }
    return $value
}
function Get-FullPath([string]$Path) { return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path) }
function Assert-Url([string]$Url) {
    if ($Url -cnotmatch '^https?://[^/?#\s]+\S*$') { throw "'$Url' is not an http(s) URL." }
}
# Follows symbolic links to the real file, like Python's os.path.realpath.
function Resolve-Link([string]$Path) {
    $Path = Get-FullPath $Path
    for ($n = 0; $n -lt 32; $n++) {
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        if ($null -eq $item -or [string]$item.LinkType -ne 'SymbolicLink') { return $Path }
        $target = [string]@($item.Target)[0]
        if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path (Split-Path -Parent $Path) $target }
        $Path = Get-FullPath $target
    }
    throw "${Path}: too many levels of symbolic links."
}
# Replaces the file in one step, through a uniquely named temporary file in the same folder.
function Write-TextAtomic([string]$Path, [string]$Text) {
    $Path = Resolve-Link $Path
    $dir = Split-Path -Parent $Path
    [void][IO.Directory]::CreateDirectory($dir)
    $tmp = Join-Path $dir ('.' + [IO.Path]::GetFileName($Path) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText($tmp, $Text, (New-Object System.Text.UTF8Encoding($false)))
        if ([IO.File]::Exists($Path)) { [IO.File]::Replace($tmp, $Path, [NullString]::Value) }
        else { [IO.File]::Move($tmp, $Path) }
    } finally {
        if ([IO.File]::Exists($tmp)) { [IO.File]::Delete($tmp) }
    }
}
# Reads UTF-8 text; a byte-order mark is dropped.
function Read-Text([string]$Path) { return [IO.File]::ReadAllText((Get-FullPath $Path)) }
function Test-FileExists([string]$Path) { return [IO.File]::Exists((Get-FullPath $Path)) }
if ($argc -gt 0 -and $arg1.Length -gt 1 -and $arg1.StartsWith('-')) { throw "Unknown option: $arg1 (see --help)" }
if ($argc -gt 0) { throw "Unexpected argument: $arg1 (this script takes no arguments; see --help)" }
$action = 'remove'
$name = 'verbex'
$url = ''
$token = ''
$headerName = ''
$headerValue = ''
if (-not (Get-Command claude -ErrorAction SilentlyContinue)) { throw "'claude' was not found. Install Claude Code: irm https://claude.ai/install.ps1 | iex (see https://code.claude.com/docs/en/setup)." }
# True when an entry was removed, false when there was none; throws on any other failure.
function Remove-ClaudeEntry {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = (& claude mcp remove --scope user $name 2>&1 | Out-String)
        $rc = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    if ($rc -eq 0) { return $true }
    if ($out -match 'No MCP server named') { return $false }
    throw "claude mcp remove failed: $($out.Trim())"
}
function Add-ClaudeEntry {
    $headerArgs = @()
    if ($headerName) { $headerArgs = @('--header', "${headerName}: $headerValue") }
    & claude mcp add --scope user --transport http $name $url @headerArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'claude mcp add failed.' }
}
if ($action -eq 'remove' -or $action -eq 'reinstall') {
    if (Remove-ClaudeEntry) { Write-Host "Removed '$name' from Claude Code at user scope." }
    elseif ($action -eq 'remove') { Write-Host "No '$name' entry in Claude Code at user scope; nothing to remove." }
    else { Write-Host "No '$name' entry in Claude Code at user scope; continuing." }
}
if ($action -eq 'install' -or $action -eq 'reinstall') {
    $replaced = $false
    if ($action -eq 'install') { $replaced = Remove-ClaudeEntry }
    Add-ClaudeEntry
    if ($replaced) { Write-Host "Replaced the existing '$name' entry in Claude Code at user scope ($url)." }
    else { Write-Host "Added '$name' to Claude Code at user scope ($url)." }
}
Write-Host 'Restart Claude Code to pick up the change.'
