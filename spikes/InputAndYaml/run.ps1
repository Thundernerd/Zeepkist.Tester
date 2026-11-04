# Deploys the spike plugin, starts Zeepkist through Steam with its flag, waits for results.json, and removes it again.
# The Windows counterpart of run.sh, for the Windows PowerShell that comes with Windows. ZEEPKIST_DIR overrides where the game is.
# Usage: powershell -ExecutionPolicy Bypass -File spikes\InputAndYaml\run.ps1 <out dir>
param(
    [Parameter(Mandatory = $true)]
    [string] $Out
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$appId = '1440670'
$build = Join-Path $PSScriptRoot 'bin/Release/net472'

function Write-SpikeLog([string] $Message) {
    Write-Host "[spike] $Message"
}

function Stop-Run([string] $Message) {
    Write-SpikeLog $Message
    exit 1
}

function Test-GameRunning {
    [bool](Get-Process -Name Zeepkist -ErrorAction SilentlyContinue)
}

# The library holding the game can be another one than Steam's own folder, which libraryfolders.vdf doesn't always list
function Find-Game([string] $SteamRoot) {
    $libraries = @($SteamRoot)
    $folders = Join-Path $SteamRoot 'steamapps/libraryfolders.vdf'
    if (Test-Path -LiteralPath $folders) {
        $found = [regex]::Matches((Get-Content -LiteralPath $folders -Raw -Encoding UTF8), '"path"\s+"([^"]+)"')
        $libraries += @($found | ForEach-Object { $_.Groups[1].Value.Replace('\\', '\') })
    }

    # Not Join-Path: it throws on a library whose drive isn't plugged in
    foreach ($library in $libraries) {
        if (Test-Path -LiteralPath "$library/steamapps/appmanifest_$appId.acf") {
            return "$library/steamapps/common/Zeepkist"
        }
    }

    return $null
}

if (Test-GameRunning) { Stop-Run 'Zeepkist is already running, close it first' }
if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) { Stop-Run "Steam isn't running" }

$steamRoot = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam').SteamPath
$game = if ($env:ZEEPKIST_DIR) { $env:ZEEPKIST_DIR } else { Find-Game $steamRoot }
if (-not $game) { Stop-Run "Zeepkist isn't in any Steam library, set ZEEPKIST_DIR to its folder" }
if (-not (Test-Path -LiteralPath (Join-Path $game 'BepInEx'))) { Stop-Run "No BepInEx folder in $game" }

$plugin = Join-Path $build 'Zeepkist.Tester.Spike.dll'
if (-not (Test-Path -LiteralPath $plugin)) {
    Stop-Run "No build at $plugin, run: dotnet build spikes\InputAndYaml\Zeepkist.Tester.Spike.csproj -c Release"
}

$deploy = Join-Path $game 'BepInEx/plugins/Sideloaded/Plugins/Zeepkist.Tester.Spike'
$pluginOut = Join-Path $deploy 'out'
$results = Join-Path $pluginOut 'results.json'
$finished = $false
$started = $false

if (Test-Path -LiteralPath (Join-Path $Out 'results.json')) { Stop-Run "$Out already has results, use another folder" }
New-Item -ItemType Directory -Force -Path $Out | Out-Null

# The game's process only shows up a while after the launch, so checking for it can't stop a second run
$isOnlyRun = $false
$lock = [System.Threading.Mutex]::new($true, 'Zeepkist.Tester.Spike', [ref]$isOnlyRun)
if (-not $isOnlyRun) { Stop-Run 'another run is going, wait for it to end' }

# A closed terminal skips the cleanup, and results.json left behind by that would count as this run's
if (Test-Path -LiteralPath $deploy) { Remove-Item -LiteralPath $deploy -Recurse -Force }

try {
    New-Item -ItemType Directory -Force -Path $deploy | Out-Null
    Copy-Item -LiteralPath $plugin, (Join-Path $build 'YamlDotNet.dll') -Destination $deploy
    $runId = Get-Date -Format 'yyyyMMdd-HHmmss'
    Set-Content -LiteralPath (Join-Path $deploy 'run.json') -Value "{`"runId`":`"$runId`"}" -NoNewline -Encoding Ascii

    Write-SpikeLog "starting the game, run $runId"
    Start-Process -FilePath (Join-Path $steamRoot 'steam.exe') -ArgumentList '-applaunch', $appId, '-ztest-spike', $runId

    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $results) {
            Write-SpikeLog 'results written'
            $finished = $true
            break
        }

        if (Test-GameRunning) {
            $started = $true
        }
        elseif ($started) {
            Write-SpikeLog 'the game quit without results'
            break
        }

        Start-Sleep -Seconds 1
    }

    if ((Get-Date) -ge $deadline) { Write-SpikeLog 'timed out' }

    for ($i = 0; $i -lt 30 -and (Test-GameRunning); $i++) {
        Start-Sleep -Seconds 1
    }
}
finally {
    try {
        if (Test-GameRunning) {
            Get-Process -Name Zeepkist -ErrorAction SilentlyContinue | ForEach-Object { [void]$_.CloseMainWindow() }
            Start-Sleep -Seconds 5
            Get-Process -Name Zeepkist -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            # Windows won't delete the plugin's DLLs until the process is gone
            Wait-Process -Name Zeepkist -Timeout 10 -ErrorAction SilentlyContinue
        }

        if (Test-Path -LiteralPath $pluginOut) { Get-ChildItem -LiteralPath $pluginOut | Copy-Item -Destination $Out -Recurse -Force }

        # The logs of a game that never started are an earlier run's. Player.log says why when the game stalled before BepInEx loaded the plugin
        if ($started) {
            $logs = @((Join-Path $game 'BepInEx/LogOutput.log'), (Join-Path $env:USERPROFILE 'AppData/LocalLow/Steelpan Interactive/Zeepkist/Player.log'))
            foreach ($logFile in $logs) {
                if (Test-Path -LiteralPath $logFile) { Copy-Item -LiteralPath $logFile -Destination $Out -Force }
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $deploy) { Remove-Item -LiteralPath $deploy -Recurse -Force }
        Write-SpikeLog "removed the spike from $deploy"
        $lock.Dispose()
    }
}

if (-not $finished) { Stop-Run "no results, see the logs in $Out" }
Write-SpikeLog "results are in $Out"
