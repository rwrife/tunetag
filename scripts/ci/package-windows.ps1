param(
    [string]$Version = "0.1.0",
    [string]$ArtifactDir = "artifacts/windows"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-MsixVersion {
    param([string]$Raw)

    $clean = ($Raw ?? "").Trim()
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return "0.1.0.0"
    }

    if ($clean.StartsWith("v")) {
        $clean = $clean.Substring(1)
    }

    $match = [regex]::Match($clean, "^(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?")
    if (-not $match.Success) {
        return "0.1.0.0"
    }

    $revision = 0
    if ($match.Groups[4].Success) {
        $revision = [int]$match.Groups[4].Value
    }

    return "{0}.{1}.{2}.{3}" -f ([int]$match.Groups[1].Value), ([int]$match.Groups[2].Value), ([int]$match.Groups[3].Value), $revision
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $repoRoot

$artifactRoot = Join-Path $repoRoot $ArtifactDir
$publishDir = Join-Path $artifactRoot "publish/win-x64"
$portableStage = Join-Path $artifactRoot "staging/portable"
$msixStage = Join-Path $artifactRoot "staging/msix"
$zipPath = Join-Path $artifactRoot "tunetag-win-x64-portable.zip"
$msixPath = Join-Path $artifactRoot "tunetag-win-x64.msix"
$msixVersion = Get-MsixVersion -Raw $Version

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $publishDir, $portableStage, $msixStage, $zipPath, $msixPath

$publishArgs = @(
    "publish", "src/TuneTag.App/TuneTag.App.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-o", $publishDir
)

Write-Host "Running: dotnet $($publishArgs -join ' ')"
dotnet @publishArgs

New-Item -ItemType Directory -Force -Path $portableStage | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $portableStage -Recurse -Force

$appExe = Join-Path $portableStage "TuneTag.App.exe"
if (-not (Test-Path $appExe)) {
    throw "Expected executable not found at $appExe"
}
Rename-Item -Path $appExe -NewName "tunetag.exe"

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path (Join-Path $portableStage "*") -DestinationPath $zipPath -CompressionLevel Optimal

New-Item -ItemType Directory -Force -Path $msixStage | Out-Null
Copy-Item -Path (Join-Path $portableStage "*") -Destination $msixStage -Recurse -Force

$assetsDir = Join-Path $msixStage "Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

# 1x1 PNG (placeholder icon for CI packaging)
$png = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2Qn9sAAAAASUVORK5CYII=")
[IO.File]::WriteAllBytes((Join-Path $assetsDir "StoreLogo.png"), $png)
[IO.File]::WriteAllBytes((Join-Path $assetsDir "Square44x44Logo.png"), $png)
[IO.File]::WriteAllBytes((Join-Path $assetsDir "Square150x150Logo.png"), $png)

$manifestPath = Join-Path $msixStage "AppxManifest.xml"
@"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity Name="rwrife.tunetag" Publisher="CN=rwrife" Version="$msixVersion" />
  <Properties>
    <DisplayName>TuneTag</DisplayName>
    <PublisherDisplayName>rwrife</PublisherDisplayName>
    <Description>TuneTag desktop music tag editor</Description>
    <Logo>Assets\\StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" />
  </Dependencies>
  <Applications>
    <Application Id="TuneTag" Executable="tunetag.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        DisplayName="TuneTag"
        Description="TuneTag desktop music tag editor"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\\Square150x150Logo.png"
        Square44x44Logo="Assets\\Square44x44Logo.png" />
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"@ | Set-Content -Path $manifestPath -Encoding UTF8

$makeAppx = Get-Command makeappx.exe -ErrorAction SilentlyContinue
if (-not $makeAppx) {
    $kitRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits/10/bin"
    if (Test-Path $kitRoot) {
        $candidate = Get-ChildItem -Path $kitRoot -Recurse -Filter makeappx.exe | Sort-Object FullName -Descending | Select-Object -First 1
        if ($candidate) {
            $makeAppx = @{ Source = $candidate.FullName }
        }
    }
}

if (-not $makeAppx) {
    throw "makeappx.exe not found in PATH or Windows Kits"
}

if (Test-Path $msixPath) {
    Remove-Item -Force $msixPath
}

Write-Host "Packing MSIX with $($makeAppx.Source)"
& $makeAppx.Source pack /d $msixStage /p $msixPath /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx failed with exit code $LASTEXITCODE"
}

Write-Host "Created artifacts:"
Write-Host " - $zipPath"
Write-Host " - $msixPath"
