param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$NoBridgeToken
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outputRoot = Join-Path $root "publish-status-push"
$enableBridgeToken = if ($NoBridgeToken) { "false" } else { "true" }

function Publish-App {
    param(
        [string]$ProjectPath,
        [string]$OutputPath
    )

    if (Test-Path $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Recurse -Force
    }

    dotnet publish $ProjectPath `
        -c $Configuration `
        -r $Runtime `
        -o $OutputPath `
        --self-contained false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:PublishReadyToRun=false `
        -p:EnableBridgeToken=$enableBridgeToken `
        -p:EnableStatusPushPrototype=true
}

Publish-App `
    -ProjectPath (Join-Path $root "RemoteMonitor.Client\RemoteMonitor.Client.csproj") `
    -OutputPath (Join-Path $outputRoot "client")
Publish-App `
    -ProjectPath (Join-Path $root "RemoteMonitor.Server\RemoteMonitor.Server.csproj") `
    -OutputPath (Join-Path $outputRoot "server")
Publish-App `
    -ProjectPath (Join-Path $root "RemoteMonitor.Server.Service\RemoteMonitor.Server.Service.csproj") `
    -OutputPath (Join-Path $outputRoot "server-service")
