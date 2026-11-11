param(
    [Parameter(Mandatory=$true)][string]$ChecksJson,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../tools/Climate.Portfolio/Climate.Portfolio.csproj'
& $DotnetPath run --project $project -c Release -- sat $ChecksJson $Output
if ($LASTEXITCODE -ne 0) { throw 'SAT generation failed.' }
