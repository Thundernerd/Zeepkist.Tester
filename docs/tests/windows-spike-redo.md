# Redoing the spike on Windows

The first Windows run (`docs/tests/spike-out/`) left doubts: the game wasn't in front at the start screen in either run, and the logs weren't kept. This redo makes four runs, each with one clear purpose, and checks each one straight away, so a bad run is redone on the spot.

It takes about 15 minutes. Each run takes a minute or two.

## The three rules

Breaking one of these can change a result without any error, so they apply to every run:

1. **Use your mouse and keyboard only where a run tells you to.** The start screen advances on any button. The game's input library (Rewired) reads input even when the game is behind another window, so typing or clicking elsewhere counts too.
2. **If you have to touch something, do it early:** only while the game is still loading, before the "press any key" screen shows. Use a mouse click, not Alt+Tab.
3. **If anything goes wrong in a run, don't try to save it.** Let it end, and do it again with a new folder name, such as `3-behind-b`. Keep the failed folder too, as it's evidence as well.

## Before you start

- Close Zeepkist. Steam must be running, and the monitor on.
- Set the game to windowed mode, if it isn't already.
- Turn off anything that can take focus: Steam's overlay notifications, chat apps, and so on.
- Have the .NET 10 SDK installed.

Open **Windows PowerShell** and go to the repo:

```powershell
git fetch origin
git switch docs/windows-spike-redo
git pull
dotnet build spikes\InputAndYaml\Zeepkist.Tester.Spike.csproj -c Release
```

The build has to end with `Build succeeded`.

Paste this into the same window. It defines the commands the runs below use. They haven't been run on Windows yet, so if one gives an error, send it to me along with whatever `results.json` the run left: the run itself is still good.

```powershell
function Start-SpikeRun($Name) {
    powershell -ExecutionPolicy Bypass -File spikes\InputAndYaml\run.ps1 "docs\tests\spike-redo\$Name"
}

function Show-SpikeRun($Name) {
    $folder = "docs\tests\spike-redo\$Name"
    $r = Get-Content "$folder\results.json" -Raw | ConvertFrom-Json
    "done:                  $($r.done)"
    "exception:             $($r.exception)"
    "in front at start:     $($r.focus.game_window_is_foreground)"
    "SetForegroundWindow:   $($r.focus.set_foreground_returned)"
    "in front after it:     $($r.focus.game_window_is_foreground_after)"
    "mouse in front:        $($r.sendinput_mouse.game_window_is_foreground)"
    "mouse Input/Rewired:   $($r.sendinput_mouse.legacy_moved) / $($r.sendinput_mouse.rewired_moved)"
    foreach ($p in 'sendinput_key_vk', 'sendinput_key_scancode', 'sendinput_space_start_screen') {
        $k = $r.$p
        "${p}:"
        "  in front:              $($k.game_window_is_foreground)"
        "  Input/Rewired/OnGUI:   $($k.legacy_input_getkeydown) / $($k.rewired_keyboard_getkeydown) / $($k.ongui_keydown)"
    }
    $s = $r.sendinput_space_start_screen
    "start screen before/after Space: $($s.start_screen_advanced_before) / $($s.start_screen_advanced)"
    "Harmony reached game code:       $($r.harmony_input.game_code_saw_patched_input)"
    "YAML ok:                         $($r.yaml.ok)"
    "logs:                            $((Test-Path "$folder\LogOutput.log")) / $((Test-Path "$folder\Player.log"))"
    "log has this run's lines:        $([bool](Select-String -Path "$folder\LogOutput.log" -Pattern 'ZTEST-SPIKE\] run \d+-\d+ starting' -Quiet))"
}
```

After each run, write down what you saw, using the template at the end.

## Run 1: hands off

**Answers:** does the game get to the front by itself, with nothing else happening?

1. Run `Start-SpikeRun 1-hands-off`.
2. **Don't touch anything** until the prompt comes back. Watch whether the game's window comes to the front, and whether it stays there.
3. Run `Show-SpikeRun 1-hands-off`.

`in front at start` can be either value. It's the answer to this run's question, so don't redo the run because of it.

## Run 2: the game clicked to the front

**Answers:** does everything work with the game in front from the start? No earlier Windows run had that: the game only got in front when the plugin brought it there.

1. Run `Start-SpikeRun 2-in-front`.
2. If the game's window doesn't come to the front by itself, click it **once**, as soon as it appears, while it's still loading. If it comes to the front by itself, don't click.
3. Then don't touch anything until the prompt comes back.
4. Run `Show-SpikeRun 2-in-front`.

This run counts only when `in front at start` is `True`. If it's `False`, redo it as `2-in-front-b` and click sooner.

## Runs 3 and 4: the game behind Notepad, twice

**Answers:** can the game bring itself to the front? It failed once and worked once before, so this run goes twice, to see if that holds.

For each of these two runs:

1. Open an empty Notepad. The spike's keys can land in it, so it should stay empty and unsaved.
2. Run the command below, then **straight away click into Notepad**.
3. When the game's window appears: if it comes in front of Notepad, click Notepad again, right away, while the game is still loading. Don't use Alt+Tab, as Rewired would read those keys.
4. Then don't touch anything until the prompt comes back, even if the game jumps to the front later. If it does, write down when.
5. Check the run.

```powershell
Start-SpikeRun 3-behind
Show-SpikeRun 3-behind
```

Then the same again:

```powershell
Start-SpikeRun 4-behind-again
Show-SpikeRun 4-behind-again
```

Each run counts only when `in front at start` is `False`. If it's `True`, the game wasn't behind Notepad when the check ran, so redo it with a `-b` name.

`SetForegroundWindow` and `in front after it` can be either value. They're the answer. When they're `True`, the input probes ran in front, and when they're `False`, they ran behind.

## What every run should show

If any of these is wrong, keep the folder and redo the run with a `-b` name. If it's wrong the same way twice, it's a finding, so note it and move on.

| Line | Should be |
| --- | --- |
| `done` | `True` |
| `exception` | empty |
| `start screen before/after Space` | `False / True`. A `True` first means something pressed a button too early (rule 1). |
| `Harmony reached game code` | `True` |
| `YAML ok` | `True` |
| `logs` | `True / True` |
| `log has this run's lines` | `True`. `False` means the log is from an earlier run. |
| `SetForegroundWindow` | Empty when `in front at start` is `True`, as it's only called when the game is behind |

These are what the runs are measuring, so no value is wrong. Just make sure each is there:

| Line | What it tells us |
| --- | --- |
| `mouse Input/Rewired` | Whether mouse moves arrive, in front or behind |
| `Input/Rewired/OnGUI` | Which input paths see keys, with `in front` above it saying where the game was |

If `Show-SpikeRun` can't find `results.json`, the run got no results. Keep the folder, as its logs say why, and redo the run. The script's own last lines say what happened, so copy them into your notes.

## Your notes

Save this as `docs\tests\spike-redo\notes.md`, with a block for each run, including redone ones:

```markdown
## 1-hands-off
- Did the game's window come to the front by itself? When?
- What was in front when the "press any key" screen showed?
- Did you touch the mouse or keyboard? When?
- Anything odd? (a popup, a notification, the script's last lines if it failed)
```

## Sending the results back

The `*.log` files are ignored by git, which is why the first run's weren't kept, so `-f` is needed:

```powershell
git add -f docs\tests\spike-redo
git commit -m "chore(spike): add Windows redo results"
git push
```

The logs include your Windows user name and your list of installed mods. That's fine while the repo is private, but they'd have to go before it's made public, as they stay in the git history.
