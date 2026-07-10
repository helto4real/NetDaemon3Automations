$ErrorActionPreference = "Stop"

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

Push-Location $PSScriptRoot
try {
    # Use an absolute repository path because dotnet-outdated resolves recursively
    # discovered project paths relative to each project directory.
    Invoke-DotNet @("outdated", "-u", "--recursive", $PSScriptRoot)

    [xml] $centralPackages = Get-Content (Join-Path $PSScriptRoot "Directory.Packages.props")
    $netDaemonPackage = $centralPackages.Project.ItemGroup.PackageVersion |
        Where-Object { $_.Include -eq "NetDaemon.AppModel" } |
        Select-Object -First 1

    if ($null -eq $netDaemonPackage) {
        throw "Could not determine the central NetDaemon package version."
    }

    $netDaemonVersion = [string] $netDaemonPackage.Version
    Invoke-DotNet @("tool", "update", "NetDaemon.HassModel.CodeGen", "--local", "--version", $netDaemonVersion)
    Invoke-DotNet @("tool", "run", "nd-codegen", "-o", "src/NetDaemonApps/HomeAssistantGenerated.cs", "-ns", "HomeAssistantGenerated")
    Invoke-DotNet @("restore", "NetDaemonApps.slnx", "--force-evaluate", "-m:1")

    Write-Host "Updated the full solution and regenerated the Home Assistant model with NetDaemon $netDaemonVersion." -ForegroundColor Green
}
finally {
    Pop-Location
}
