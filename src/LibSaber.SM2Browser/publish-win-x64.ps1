$project = Join-Path $PSScriptRoot "LibSaber.SM2Browser.csproj"
dotnet publish $project -c Release -p:PublishProfile=win-x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
