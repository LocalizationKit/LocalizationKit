# Builds the VSIX without needing to know where Visual Studio is installed.
#
#   pwsh tools\LocalizationKit.VsExtension\build.ps1            # Release, the one to install
#   pwsh tools\LocalizationKit.VsExtension\build.ps1 -Configuration Debug
#
# A VSIX is a .NET Framework project built by the VS SDK's own targets, so `dotnet build` cannot do
# it: this finds Visual Studio's MSBuild and calls that.
[CmdletBinding()]
param(
  [ValidateSet("Debug", "Release")]
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
  throw "vswhere.exe not found at $vswhere. Visual Studio 2022 or newer has to be installed to build a VSIX."
}

# MSBuild is all that is asked for: the VSIX targets themselves arrive with the
# Microsoft.VSSDK.BuildTools package the project references, so the extension development workload
# is not needed to build — only to debug into an experimental instance.
$msbuild = & $vswhere -latest -prerelease -products * `
  -requires Microsoft.Component.MSBuild `
  -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1

if (-not $msbuild) {
  throw @"
No Visual Studio install with MSBuild was found.
Install Visual Studio 2022 or newer, or the Build Tools, and try again.
"@
}

Write-Host "Building with $msbuild" -ForegroundColor Cyan

$project = Join-Path $PSScriptRoot "LocalizationKit.VsExtension.csproj"
& $msbuild $project -t:Restore -p:Configuration=$Configuration -verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }

# Rebuild, not Build. The VSIX targets decide the container is up to date from the assembly alone,
# so a change to source.extension.vsixmanifest — the display name, the version, the icon — leaves
# yesterday's manifest inside today's .vsix, and the extension installs under its old name. The
# project is small enough that always rebuilding costs nothing worth having.
& $msbuild $project -t:Rebuild -p:Configuration=$Configuration -verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$vsix = Join-Path $PSScriptRoot "bin\$Configuration\LocalizationKit.VsExtension.vsix"
if (-not (Test-Path $vsix)) { throw "The build reported success but $vsix is not there." }

Write-Host ""
Write-Host "Built $vsix" -ForegroundColor Green
Write-Host "Double-click it to install, then restart Visual Studio." -ForegroundColor Green
