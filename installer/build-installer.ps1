param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$Token
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$issPath = Join-Path $PSScriptRoot "RemoteMonitor_Setup.iss"
$enableBridgeToken = if ($Token) { "true" } else { "false" }
$publishDirectoryName = if ($Token) { "publish-token" } else { "publish" }
$publishRoot = Join-Path $root $publishDirectoryName

function Publish-App {
    param(
        [string]$ProjectPath,
        [string]$OutputPath
    )

    if (Test-Path $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $OutputPath | Out-Null

    $commonArgs = @(
        "publish", $ProjectPath,
        "-c", $Configuration,
        "-r", $Runtime,
        "-o", $OutputPath,
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:PublishReadyToRun=false",
        "-p:EnableBridgeToken=$enableBridgeToken"
    )

    if ($SelfContained) {
        $publishArgs = $commonArgs + @(
            "--self-contained", "true",
            "-p:PublishSingleFile=true",
            "-p:IncludeNativeLibrariesForSelfExtract=true",
            "-p:EnableCompressionInSingleFile=true"
        )
    }
    else {
        $publishArgs = $commonArgs + @(
            "--self-contained", "false",
            "-p:PublishSingleFile=false"
        )
    }

    dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for '$ProjectPath' with exit code $LASTEXITCODE."
    }
}

Publish-App -ProjectPath (Join-Path $root "RemoteMonitor.Client\RemoteMonitor.Client.csproj") -OutputPath (Join-Path $publishRoot "client")
Publish-App -ProjectPath (Join-Path $root "RemoteMonitor.Server\RemoteMonitor.Server.csproj") -OutputPath (Join-Path $publishRoot "server")
Publish-App -ProjectPath (Join-Path $root "RemoteMonitor.Server.Service\RemoteMonitor.Server.Service.csproj") -OutputPath (Join-Path $publishRoot "server-service")

$bridgeProject = Join-Path $root "RemoteMonitor.Bridge\RemoteMonitor.Bridge.csproj"
if (Test-Path $bridgeProject) {
    Publish-App -ProjectPath $bridgeProject -OutputPath (Join-Path $publishRoot "bridge")
}

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$isccPath = if ($iscc) { $iscc.Source } else { $null }

if (-not $isccPath) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            $isccPath = $candidate
            break
        }
    }
}

if (-not $isccPath) {
    throw "ISCC.exe was not found. Install Inno Setup 6, then run this script again."
}

$isccArgs = @()
if (-not $SelfContained) {
    $isccArgs += "/DFrameworkDependent=1"
}
if ($Token) {
    $isccArgs += "/DTokenBuild=1"
}
$isccArgs += $issPath
& $isccPath @isccArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}


