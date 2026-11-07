#!/usr/bin/env bash
# Runs this plugin's scenarios in the real game through Steam, and compares the results with expected-results.txt. It
# puts back what it deploys and what a run changes, until the runner (#22, #23) does this for every mod.
# Usage: Zeepkist.Tester.Tests.InGame/run.sh <out dir> [--no-scenarios|--start-fails]
# With --no-scenarios it checks a run with the right builds and no scenarios fails, saying so.
# With --start-fails it checks a start that fails ends the run, with what's left listed as not run.
set -euo pipefail

app_id=1440670
guid=Zeepkist.Tester.Tests.InGame
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(dirname "$here")"
out="${1:?out dir}"
mode="${2:-}"
[[ "$mode" =~ ^(|--no-scenarios|--start-fails)$ ]] || { printf 'Unknown option %s\n' "$mode"; exit 1; }
game="${ZEEPKIST_DIR:-$HOME/.steam/steam/steamapps/common/Zeepkist}"
plugins="$game/BepInEx/plugins/Sideloaded/Plugins"
tester="$plugins/Zeepkist.Tester"
mod="$plugins/$guid"
app_data="$HOME/.steam/steam/steamapps/compatdata/$app_id/pfx/drive_c/users/steamuser/AppData"
data="$app_data/Roaming/Zeepkist"
low="$app_data/LocalLow/Steelpan Interactive/Zeepkist"
log() { printf '[ingame] %s\n' "$*"; }

# What a run writes to (#60), each put back from a copy afterwards
changed=(
    "$data/Autosaves" "$data/Backups" "$data/Mods"
    "$low/Technical/Backup" "$low/Belangrijk/Backups" "$low/DeletedBackups" "$low/LevelIndexes"
    "$game/BepInEx/cache" "$game/BepInEx/LogOutput.log"
)

game_running() {
    pgrep -x Zeepkist.exe >/dev/null || pgrep -f "^\\S*/reaper SteamLaunch AppId=$app_id " >/dev/null
}

# Shared with other in-game runs, so they never run alongside each other
exec 9>"${XDG_RUNTIME_DIR:-/tmp}/zls-ingame-test.lock"
flock -n 9 || { log "waiting for another in-game run"; flock 9; }
game_running && { log "Zeepkist is already running, close it first"; exit 1; }
pgrep -x steam >/dev/null || { log "Steam isn't running"; exit 1; }
[[ ! -e "$tester" && ! -e "$mod" ]] || { log "$tester or $mod is already there, and isn't this script's to replace"; exit 1; }

# The compiler server outlives the build, and would keep holding the lock after this script ends
dotnet build "$repo" --configuration Release --nologo --verbosity quiet 9>&-
dotnet "$repo/Zeepkist.Tester.Cli/bin/Release/net10.0/Zeepkist.Tester.Cli.dll" check \
    "$here/Scenarios"/*.ztest.yaml --plugin "$here/bin/Release/net472/$guid.dll" 9>&-

mkdir -p "$out"
kept="$(mktemp -d "${TMPDIR:-/tmp}/ztest-kept.XXXXXX")"
for i in "${!changed[@]}"; do
    [[ -e "${changed[$i]}" ]] && cp -a "${changed[$i]}" "$kept/$i"
done

cleanup() {
    # One step failing shouldn't keep the others from putting things back
    set +e
    if game_running; then
        pkill -x Zeepkist.exe || true
        sleep 5
        pkill -9 -x Zeepkist.exe || true
    fi
    [[ -d "$tester/out" ]] && cp -a "$tester/out/." "$out/"
    [[ -f "$game/BepInEx/LogOutput.log" ]] && cp "$game/BepInEx/LogOutput.log" "$out/"
    [[ -f "$low/Player.log" ]] && cp "$low/Player.log" "$out/"
    rm -rf "$tester" "$mod"
    for i in "${!changed[@]}"; do
        if [[ -e "$kept/$i" ]]; then
            rm -rf "${changed[$i]}"
            cp -a "$kept/$i" "${changed[$i]}"
        else
            rm -rf "${changed[$i]}"
        fi
    done
    rm -rf "$kept"
    log "put the game's install back"
}
trap cleanup EXIT

mkdir -p "$tester" "$mod"
cp "$repo/Zeepkist.Tester/bin/Release/net472/plugin/"* "$tester/"
cp "$here/bin/Release/net472/$guid.dll" "$mod/"
run_id="$(date +%Y%m%d-%H%M%S)"
cp -a "$here/Scenarios" "$tester/scenarios"
case "$mode" in
    --no-scenarios) scenarios='[]' ;;
    --start-fails) scenarios='["core-steps.ztest.yaml","start-fails.ztest.yaml","control-nodes.ztest.yaml"]' ;;
    *) scenarios='["core-steps.ztest.yaml","a-step-fails.ztest.yaml","control-nodes.ztest.yaml","screenshots.ztest.yaml","exceptions.ztest.yaml","blocks.ztest.yaml","test-mode.ztest.yaml","test-mode-needs-a-start.ztest.yaml","a-setting-it-does-not-have.ztest.yaml","imui-input.ztest.yaml"]' ;;
esac
printf '{"runId":"%s","mods":[{"guid":"%s"}],"scenarios":%s}' "$run_id" "$guid" "$scenarios" >"$tester/run.json"

log "starting the game, run $run_id"
steam -applaunch "$app_id" -ztest "$run_id" >/dev/null 2>&1 9>&- &
disown

results="$tester/out/results.json"
deadline=$((SECONDS + 420))
started=0
finished=0
while ((SECONDS < deadline)); do
    if [[ -f "$results" ]] && jq -e .Done "$results" >/dev/null 2>&1; then
        log "results written"
        finished=1
        break
    fi
    if game_running; then
        started=1
    elif ((started)); then
        log "the game quit without finishing"
        break
    fi
    sleep 1
done
((finished)) || { log "no finished results; see $out"; exit 1; }

for _ in $(seq 30); do
    game_running || break
    sleep 1
done

jq -r '.Steps[] | "\(.Scenario):\(.Line)\(if .RanFrom != "" then " in a run: file" else "" end) \(.Name) \(if .Passed then "pass" else "FAIL" end): \(.Detail)"' "$results"
jq -r '.Exceptions[]? | "exception in \(.Scenario): \(.Message)"' "$results"

failed=0
if [[ "$mode" == --no-scenarios ]]; then
    jq -e '.Done and (.Passed | not) and ([.Steps[] | select(.Passed | not)] | length == 1) and (.Steps[] | select(.Passed | not) | .Name == "scenarios")' "$results" >/dev/null || failed=1
elif [[ "$mode" == --start-fails ]]; then
    jq -e '.Done and (.Passed | not)
        and ([.Steps[] | select(.Passed | not) | "\(.Scenario)/\(.Name)"] == ["start-fails/start: free-play", "control-nodes/not run"])
        and (.Screenshots | index("start-fails-failure.png"))' "$results" >/dev/null || failed=1
else
    jq -r '.Steps[] | "\(.Name) | \(.Scenario) | \(if .Passed then "pass" else "FAIL" end)"' "$results" >"$out/actual-results.txt"
    diff "$here/expected-results.txt" "$out/actual-results.txt" || failed=1
    jq -e '(.Screenshots | index("a-step-fails-failure.png")) and (.Screenshots | index("control-nodes-height-64.png")) and (.BlankScreenshots | keys == ["screenshots-covered.png"])' "$results" >/dev/null || { log "the screenshots aren't what they should be"; failed=1; }
fi

((failed == 0)) || { log "not what this run should do"; exit 1; }
log "the run did what it should"
