# Publishes a self-contained single-file win-x64 build.
# The output folder does not require a .NET install.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'LibSaber.SM2Extractor.csproj'
dotnet publish $project -c Release -p:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

$published = Join-Path $PSScriptRoot 'bin\Release\net8.0\win-x64\publish'
Write-Host "Published to $published"
