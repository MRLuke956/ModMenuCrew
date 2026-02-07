param(
  [Parameter(Mandatory = $false)]
  [string]$Target = "Build"
)

$ErrorActionPreference = "Stop"

dotnet tool restore | Out-Host
dotnet tool run dotnet-cake -- build.cake --target=$Target | Out-Host

