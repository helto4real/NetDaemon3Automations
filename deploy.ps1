param(
    [Parameter()]
    [string] $Destination = "/mnt/homeassistant/nd5",

    [Parameter()]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src/NetDaemonApps/NetDaemonApps.csproj"
$solution = Join-Path $PSScriptRoot "NetDaemonApps.slnx"
$publishDirectory = Join-Path $PSScriptRoot "artifacts/publish"

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

Invoke-DotNet @("restore", $solution, "--locked-mode", "-m:1")
Invoke-DotNet @("test", $solution, "--configuration", $Configuration, "--no-restore")
Invoke-DotNet @("publish", $project, "--configuration", $Configuration, "--no-restore", "--output", $publishDirectory)

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $Destination -Recurse -Force

Write-Host "Published NetDaemon to $Destination" -ForegroundColor Green
