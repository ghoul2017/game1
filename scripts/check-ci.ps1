[CmdletBinding()]
param(
    [switch]$Full
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$gitCommand = Get-Command git -ErrorAction Stop
$gitRoot = Split-Path (Split-Path $gitCommand.Source -Parent) -Parent
$gitBash = Join-Path $gitRoot 'bin\bash.exe'

if (-not (Test-Path -LiteralPath $gitBash))
{
    throw "Git for Windows Bash was not found: $gitBash"
}

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$targetScript = if ($Full) { 'scripts/check-all' } else { 'scripts/check-ci' }
$ciExitCode = 1

Push-Location $repositoryRoot
try
{
    Write-Host "Running $targetScript with Git Bash"
    & $gitBash --noprofile --norc $targetScript
    $ciExitCode = $LASTEXITCODE
}
finally
{
    Pop-Location
}

if ($ciExitCode -ne 0)
{
    exit $ciExitCode
}
