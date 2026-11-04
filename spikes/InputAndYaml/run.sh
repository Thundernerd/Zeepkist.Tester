#!/usr/bin/env bash
# Deploys the spike plugin, starts Zeepkist through Steam with its flag, waits for results.json, and removes it again.
# Usage: spikes/InputAndYaml/run.sh <out dir>
set -euo pipefail

app_id=1440670
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
out="${1:?out dir}"
game="${ZEEPKIST_DIR:-$HOME/.steam/steam/steamapps/common/Zeepkist}"
deploy="$game/BepInEx/plugins/Sideloaded/Plugins/Zeepkist.Tester.Spike"
log() { printf '[spike] %s\n' "$*"; }

game_running() {
    pgrep -x Zeepkist.exe >/dev/null || pgrep -f "^\\S*/reaper SteamLaunch AppId=$app_id " >/dev/null
}

# Shares the ZLS in-game test lock, so it never runs alongside one
exec 9>"${XDG_RUNTIME_DIR:-/tmp}/zls-ingame-test.lock"
flock -n 9 || { log "waiting for another in-game run"; flock 9; }
game_running && { log "Zeepkist is already running, close it first"; exit 1; }
pgrep -x steam >/dev/null || { log "Steam isn't running"; exit 1; }

cleanup() {
    if game_running; then
        pkill -x Zeepkist.exe || true
        sleep 5
        pkill -9 -x Zeepkist.exe || true
    fi
    [[ -d "$deploy/out" ]] && cp -a "$deploy/out/." "$out/"
    [[ -f "$game/BepInEx/LogOutput.log" ]] && cp "$game/BepInEx/LogOutput.log" "$out/"
    rm -rf "$deploy"
    log "removed the spike from $deploy"
}
trap cleanup EXIT

mkdir -p "$out" "$deploy"
cp "$here/bin/Release/net472/Zeepkist.Tester.Spike.dll" "$here/bin/Release/net472/YamlDotNet.dll" "$deploy/"
run_id="$(date +%Y%m%d-%H%M%S)"
printf '{"runId":"%s"}' "$run_id" >"$deploy/run.json"

log "starting the game, run $run_id"
steam -applaunch "$app_id" -ztest-spike "$run_id" >/dev/null 2>&1 9>&- &
disown

deadline=$((SECONDS + 240))
started=0
while ((SECONDS < deadline)); do
    if [[ -f "$deploy/out/results.json" ]]; then
        log "results written"
        break
    fi
    if game_running; then
        started=1
    elif ((started)); then
        log "the game quit without results"
        break
    fi
    sleep 1
done
((SECONDS < deadline)) || log "timed out"

for _ in $(seq 30); do
    game_running || break
    sleep 1
done
