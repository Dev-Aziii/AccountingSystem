[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
$apiProject = Join-Path $repoRoot 'AccountingSystem.Api\AccountingSystem.Api.csproj'

if (-not (Test-Path -Path $apiProject))
{
    throw "Could not find API project at '$apiProject'. Run this script from the repository checkout."
}

$dropArguments = @(
    'ef', 'database', 'drop',
    '--context', 'AccountingDbContext',
    '--project', $apiProject,
    '--startup-project', $apiProject,
    '--force'
)

if ($NoBuild)
{
    $dropArguments += '--no-build'
}

$displayCommand = 'dotnet ' + ($dropArguments -join ' ')
Write-Verbose "Executing: $displayCommand"

$databaseDropped = $false

if ($PSCmdlet.ShouldProcess('development database', 'Drop shared SQL database via AccountingDbContext'))
{
    & dotnet @dropArguments

    if ($LASTEXITCODE -ne 0)
    {
        throw "Database drop failed. Review the EF tooling output above."
    }

    $databaseDropped = $true
}

if (-not $databaseDropped)
{
    Write-Host ''
    Write-Host 'No database changes were made.' -ForegroundColor Yellow
    return
}

Write-Host ''
Write-Host 'Database drop completed.' -ForegroundColor Green
Write-Host ''
Write-Host 'Next step:' -ForegroundColor Cyan
Write-Host '  dotnet run --project AccountingSystem.Api/AccountingSystem.Api.csproj'
Write-Host ''
Write-Host 'When the API starts, it will:'
Write-Host '  1. apply AccountingDbContext migrations'
Write-Host '  2. apply IdentityAuthDbContext migrations'
Write-Host '  3. run the bootstrap-only DataSeeder'
Write-Host ''
Write-Host 'Note: configure BootstrapAdmin:* before the first run if the database has no super-admin yet.' -ForegroundColor Yellow
