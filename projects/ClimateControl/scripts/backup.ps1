param(
    [Parameter(Mandatory=$true)][string]$Database,
    [Parameter(Mandatory=$true)][string]$Backup,
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../tools/Climate.Portfolio/Climate.Portfolio.csproj'
& $DotnetPath run --project $project -c Release -- backup $Database $Backup
if ($LASTEXITCODE -ne 0) { throw 'Backup failed.' }
