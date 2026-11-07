# A generic in-game tester for Zeepkist mods

Research into turning ZeepLogicScript's in-game tester into a setup any mod author can use, with test scripts in Lua (Zua) as well as the line format, and with real keyboard and mouse input.

Sources: `ZeepLogicScript` at `4791fba`-era main (`Zeepkist.LogicScripting.InGameTests/`, `tools/ingame-test.sh`, `docs/ingame-tests.md`), ZeepSDK at `87f7683`, the game source (Unity 2021.3.10f1), and the Rewired settings in its `Rewired Input Manager.prefab`.

## 1. What exists today

The tester has three layers:

| Layer | Where | What it does |
| --- | --- | --- |
| Host runner | `tools/ingame-test.sh` (≈560 lines of bash) | Takes a lock so only one run uses the game. Builds, finds the game in Steam libraries and its Proton prefix, sets aside other copies of the mod, deploys the build with hashes, backs up mod data/config and `Autosaves`/`Backups`, launches with `-zls-ingame-test <runId>`, waits for `results.json`, times out, restores everything (also after Ctrl+C or a killed run), and writes `results.md` including the exceptions logged at quit. |
| In-game harness | `Plugin`, `RunRequest`, `TestRun`, `RunResults`, `ExceptionLog`, `Screenshots`, `BlankCapture`, `GameEvents`, `GuardedRoutine` | Only runs with both the flag and a matching `run.json`. Walks start screen → main menu → new editor level, runs each Scenario on a fresh level, catches exceptions per step, takes screenshots (and spots blank ones), and quits. |
| Steps | `StepList`, `ScenarioParser`, `StepArgument`, `Steps/*` (≈70 steps) | A line-based `.zlstest` DSL. Each step is a coroutine (`ScenarioStep.Run(StepContext)`) that calls `context.Pass/Fail`. |

Most of it is already generic. These are the places tied to Logic Scripting:

- **Runner:** `mod_guid`, `build_files`, the mod data path `Mods/<guid>`, the `find -iname` patterns for old copies, the `[ZLS-TEST]` marker and the `-zls-ingame-test` flag are all hard-coded.
- **`BuildCheck`:** checks only the Logic Scripting DLL.
- **`TestRun.RunScenarios`:** gets the Logic Scripting plugin instance from `Chainloader.PluginInfos`, builds a `ScriptWindow.Driver`, and calls `DeleteLibraries` / `PutBackKeptState` between Scenarios. Those calls are a mod-specific reset hook.
- **`StepContext`:** carries `ScriptWindow.Driver`, `PlacedBlockNames` and Logic Scripting checks (`RequireScript`, `RequireBlockType`, `WaitForShownList`, …).
- **`StepList`:** a single static dictionary, so nobody else can add steps.
- **Start state:** always "a new, empty level in the editor".

Steps that are already generic in substance: `wait`, `log`, `screenshot`, `expect-blank-screenshot`, `expect-no-exceptions`, `press`, `type`, `undo`, `redo`, `place-start`, `place-block`, `set-block-setting`, `test-mode`, `back-to-editor`, `hover` (the mouse part).

## 2. How the game reads input, and what the tester reaches

The game reads input in four separate places, so "send a keypress" means a different thing depending on who is listening:

| Input path | Who reads it | Today's tester |
| --- | --- | --- |
| **Rewired** (actions → `InputActionScriptableObject.buttonDown/Held/axis`, filled by `InputPlayerScriptableObject.OnInputAction`) | Driving, the editor's bound keys (undo, redo, test, …), the pause menu | Only undo/redo: `EditorKeys` sets `buttonDown` in a Harmony prefix/postfix around `LEV_UndoRedo.Update`. One patch per consumer. |
| **Legacy `UnityEngine.Input`** (≈250 call sites; `activeInputHandler: 0`, so no new Input System and no `Unity.InputSystem.dll`) | The level editor's mouse (`LEV_ClickScript`, `LEV_GizmoHandler`, `LEV_ConnectorTool`, …), Escape checks, ZeepSDK's own hotkeys (`ConfigEntry<KeyCode>`) | Never reached ("Unity's own `Input` never sees these keys"). |
| **Imui** (Unity's `OnGUI` events → `ImuiUnityGUIBackend`) | ZeepSDK windows and the Script window | `ImuiKeys` pushes `ImKeyboardEvent`s onto `nextKeyboardEvents`; `ImuiMouse` patches `GetMousePosition` and queues `ImMouseEvent`s. |
| **uGUI EventSystem** | Main menu and other menus | The walk calls `GenericButton.onClick.Invoke()` directly. |

Rewired is set to Windows primary source 0 (Raw Input), with `useNativeKeyboard: 1` and `useNativeMouse: 1`. It reads the keyboard and mouse from Win32 Raw Input itself, not through Unity.

ZeepSDK doesn't inject input. `ControlsApi` / `InputOverrideStack` only turn Rewired map categories on and off, and `LevelEditorApi.Block*Input` only block input.

### Options for "real" input

There are three tiers. A generic tester should offer all of them behind one input API, with the tier chosen per call or per Scenario.

**Tier 1 – semantic.** Call the same code the button calls, like `ScriptWindow.Driver` does. Deterministic and fast, but it doesn't test the input handling itself. This stays the job of each mod's own steps.

**Tier 2 – in-process synthetic input, one adapter per path.** Deterministic, works without window focus, and doesn't touch the real cursor.
- *Imui:* generalise `ImuiKeys`/`ImuiMouse` as they are.
- *Rewired actions:* instead of patching each consumer like `EditorKeys`, set the action's fields after Rewired's update and before the game's `Update` reads them. One option is a Harmony postfix on Rewired's input-event dispatch or on `InputPlayerScriptableObject.OnInputAction`. Another is an early-execution-order MonoBehaviour that applies a queue of "action X down this frame / held for N frames / axis = v", plus a late one that clears `buttonDown`/`buttonUp`. Steps are written against action names (`Undo`, `Steer`, `Brake`, …) taken from `InputRegister.Inputs`, so key bindings don't matter.
  - Rewired's own route for virtual devices is a `CustomController`. It needs a controller definition and maps in the Input Manager's editor data, which the game doesn't ship, so this is probably unavailable without building `ControllerMap`s at runtime. It needs a spike before relying on it.
- *Legacy `Input`:* Harmony prefixes on `Input.GetKey/GetKeyDown/GetKeyUp(KeyCode|string)`, `GetMouseButton*`, `mousePosition`, `GetAxis*`, `anyKey*`. They return the synthetic state, merged with the real one or in place of it. The risk is that these are thin managed wrappers over icalls, which Mono can inline into callers. Spike: patch `GetKeyDown` and `mousePosition` and check that `LEV_ClickScript` sees them.
- *uGUI:* `ExecuteEvents.Execute(go, pointerEventData, ExecuteEvents.pointerClickHandler)` on a raycast hit at a screen point. That way "click at (x, y)" tests what is actually under the cursor.

**Tier 3 – OS-level input.** Call Win32 `SendInput` by P/Invoke from inside the game process. This is the only option that reaches all four paths at once, the way a player's hardware does:
- On Windows, `SendInput` produces window messages (which Unity's `Input` and `OnGUI` read) and Raw Input (which Rewired's native keyboard and mouse read).
- Under Proton, Wine implements `SendInput` through its own hardware-message path. Whether it also produces `WM_INPUT` for Rewired depends on the Wine/Proton version. **This needs a quick test in game first:** `SendInput` an `R` in test mode and see if the soapbox resets.
- Mouse: `SendInput` with `MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE` over the game window's client rect, or `SetCursorPos` + button events.
- Costs:
  - The game window must be in the foreground, so the run must stop the window losing focus (Steam overlay, notifications).
  - The real cursor moves, so the machine can't be used meanwhile, which the runner already asks for.
  - Input arrives a frame or more later, not at a known frame. Steps have to wait for something they can observe rather than count frames.
  - It runs at the OS key level, so US vs non-US keyboard layouts matter again. Use scan codes (`KEYEVENTF_SCANCODE`) for keys, and `KEYEVENTF_UNICODE` for `type`.
- Doing this from the host instead (xdotool/ydotool/uinput on Linux, or a ViGEm virtual gamepad on Windows) adds system dependencies per OS and per compositor. In-process `SendInput` does the same job with none of that.

Recommendation: one `IInputDriver` interface with `Imui`, `InProcess` (Rewired actions + legacy `Input` + uGUI) and `Os` (SendInput) drivers. `press`/`type`/`click`/`move`/`drag`/`hold` all go through it. A Scenario uses the in-process driver by default and can switch to the OS driver (`input os` / `input.use("os")`), which is opt-in for true end-to-end checks.

## 3. Zua and Lua scenarios

What ZeepSDK's Zua offers (MoonSharp 2.0.0):
- Each `.lua` file gets its own `Script` with `CoreModules.Preset_SoftSandbox`. Mods add functions through `ScriptingApi.RegisterFunction<T>()`, where `T : ILuaFunction` (`Namespace.Name = delegate`), and types through `RegisterType<T>()`.
- Events work by `Zua.ListenTo("Api_OnX")`, which calls a Lua global of the same name. A mod's own `ILuaEvent` can only broadcast to every script, because `IZuaBoundEvent` is internal.
- **No call can span frames.** `LuaExecutionBudget.Execute` resumes every call's coroutine in a tight loop inside the same C# call, up to about 1M instructions, so a `coroutine.yield()` doesn't suspend until the next frame. There's no `OnUpdate`, and `Zua.Script` is private.
- The source generators aren't in the NuGet package, so mods write their `ILuaFunction` classes by hand.

A test script needs `wait(seconds)`, `wait_frames(n)` and `wait_until(fn, timeout)`, so Zua can't run Scenarios as it is. There are two ways forward:

**A. The tester owns its own MoonSharp `Script` for Scenarios.**
- Use the same MoonSharp DLL ZeepSDK ships and the same sandbox preset.
- Run the Scenario's body as a MoonSharp coroutine resumed from the harness's Unity coroutine, once per frame or after its wait ends.
- Waiting functions are CLR callbacks that return `DynValue.NewYieldReq(...)`. The harness reads what the yield asked for (seconds, frames, condition) and resumes when it holds. Each resume gets its own instruction budget, so a runaway loop still fails the step instead of freezing the game.
- Restriction: MoonSharp can't yield through a CLR boundary, so `wait` can't be called inside `pcall`, a metamethod or a CLR callback. Document that, and offer `test.try(fn)` built on the coroutine instead of `pcall`.
- This works today without changing ZeepSDK. The downside is that it doesn't see the functions other mods registered with Zua.

**B. Add frame-spanning coroutines to Zua in ZeepSDK** (you own it), then build the tester on top. A small upstream change:
- an opt-in "long-running" script kind, whose coroutine Zua keeps and resumes from a PlayerLoop/`ConditionTicker` tick;
- a public way for a CLR function to yield (a `ZuaYield` helper wrapping `NewYieldReq`);
- making `IZuaBoundEvent` (or an equivalent) public, so a mod's events reach only the script that listens.

Every mod's Zua API then works inside tests, and the same waiting helps ordinary Zua scripts too (timers, "after the round starts …").

Recommendation: **start with A, and design it so it can move onto B.** Keep the yield protocol (`{ kind = "seconds", value = 2 }` etc.) and the function registration the same, then move it into Zua once the API has settled. The step table's names and behaviour don't depend on which one runs the Lua.

**One registry, two front ends.** Every step a mod registers in C# is available both to the line format and, generated automatically, to Lua:

```lua
-- editor-roundtrip.lua
test.start("editor")                -- start state: new empty editor level
zls.open_window()
zls.new_script("Lights")
zls.set_text([[power_display(1 & 1)]])
zls.compile()
expect.contains(zls.result(), "Placed 3 blocks")
editor.place_start()
editor.test_mode()
input.hold("W", 1.5)                -- in-process Rewired action by default
input.use("os"); input.press("R")   -- real key, via SendInput
test.wait_until(function() return racing.has_reset() end, 3)
test.screenshot("after-reset")
editor.back_to_editor()
expect.no_exceptions()
```

The line format turns into the same calls (`zls.compile`, `wait 2` → `test.wait(2)`), so `.zlstest` files keep working. Lua adds loops, variables and helper functions, and lets a check read values (`zls.result()`) rather than only assert them.

## 4. Proposed shape

```
Zeepkist.Tester                (BepInEx plugin, depends on ZeepSDK)
  Run gating (flag + run.json + run id), results.json/md, JUnit XML, exceptions,
  screenshots + blank check, display info
  Start states: main-menu, editor:new, editor:level <file>, free-play <uid>
    (ZeepSDK's RacingApi.LoadTrackInFreePlayAsync)
  Core steps: wait, log, screenshot, expect-no-exceptions, test-mode, back-to-editor,
    place-start, place-block, set-block-setting, undo/redo, menu clicks
  Input: IInputDriver (Imui / InProcess / Os) + press/type/click/move/drag/hold
  Lua runner (option A), line-format parser, StepArgument (public)

Zeepkist.Tester.Sdk            (NuGet, referenced by a mod's test plugin)
  ITestStep / IStepContext, TesterApi.RegisterStep(namespace, name, factory),
  TesterApi.RegisterLuaModule, scenario hooks (BeforeScenario / AfterScenario /
  AfterRun for resets like DeleteLibraries/PutBackKeptState), IBuildCheck

<Mod>.InGameTests              (each mod's own, like today's, but only its steps)
  [BepInDependency("Zeepkist.Tester")] + the mod's GUID; registers e.g. the "zls"
  namespace with its Driver-based steps and its reset hook

zeepkist-test                  (host CLI, dotnet tool; replaces the bash script)
  reads zeepkist-test.json in the mod repo; Windows (native Steam) + Linux (Proton)
```

`zeepkist-test.json` holds what is hard-coded in the bash script today:

```json
{
  "mods": [{ "guid": "Zeepkist.LogicScripting", "project": "Zeepkist.LogicScripting/Zeepkist.LogicScripting.csproj",
             "files": ["*.dll", "*.pdb"], "oldGuids": ["net.tnrd.zeepkist.logicscripting"] }],
  "testPlugin": "Zeepkist.LogicScripting.InGameTests/Zeepkist.LogicScripting.InGameTests.csproj",
  "backup": { "modData": true, "configs": true, "levelSaves": ["Autosaves", "Backups"] },
  "scenarios": "Zeepkist.LogicScripting.InGameTests/Scenarios",
  "timeout": 300
}
```

Things to keep from the current runner, which took effort to get right:
- the lock between worktrees;
- hash-marked deploys, so a player's copy is never deleted;
- putting things back after a killed run, starting from the backup;
- reading the exceptions at quit from the copied log;
- flushing BepInEx's disk log before quitting;
- telling apart "game never started" (Steam's `AppError_*`) from "the plugin wrote nothing".

Under Windows the Proton prefix becomes `%AppData%/Zeepkist`, and the launch uses `steam://run/1440670//-ztest <id>/` or `steam.exe -applaunch`.

Other mods depend on ZeepSDK too, so the runner should deploy the ZeepSDK build named in the manifest, or check the installed one. Otherwise a run tests against whatever version the player happens to have.

## 5. Risks and spikes, in order

1. ~~**`SendInput` under Proton reaching Rewired**~~ **Works**, see §5a.
2. ~~**Harmony on `UnityEngine.Input`** being inlined away in callers~~ **Works**, see §5a.
3. **Rewired action injection timing:** a generic point after Rewired's update and before the game's `Update`, without a patch per consumer.
4. **MoonSharp yield from a CLR callback** inside nested calls, and how much of Zua's registered API can be reused from a separate `Script` (option A). UserData is a single shared registry, so registered types carry over. Functions don't, because `ZuaFunctionCache` is internal.
5. ~~**Start states other than the editor.**~~ **Works**, through #12: see *Where a scenario starts* in [step-registry.md](../design/step-registry.md). Online/multiplayer is out of scope (Photon, other players).

## 5a. Spike results (2026-11-04, Linux + Proton Experimental)

`spikes/InputAndYaml/` is a throwaway plugin with `run.sh` and `run.ps1` to run it, to be deleted once #2 has run it on Windows: this section keeps the results and git history keeps the code. The plugin runs only with `-ztest-spike <id>` and a matching `run.json`. It probes at the start screen and main menu, writes `results.json` and quits. Every probe passed:

| Probe | Result |
| --- | --- |
| YamlDotNet 18.1.0 (`net47` build) next to the plugin | Loads (assembly version 18.0.0.0). It's the only YamlDotNet in the process among the installed mods. Every step node has its line and column. `on` stays the string `'on'` in the representation model. A `\|` block keeps a nested `end`. A malformed file gives `line 3 col 5: While parsing a block mapping, did not find expected key.` |
| `SendInput` absolute mouse move to 25%/75% of the client rect | Legacy `Input.mousePosition` and Rewired `Mouse.screenPosition` both land within 1 px of the target. This works with the window on a second monitor (screen x 2080). |
| `SendInput` F9, virtual-key only / scan code only | Seen by all of `Input.GetKey/GetKeyDown`, Rewired `Keyboard.GetKey/GetKeyDown`, `ReInput.controllers.GetAnyButtonDown()` and `OnGUI` `KeyDown`. |
| `SendInput` Space at the start screen | Seen the same way. The game's own `PressAnyKeyToStartGame.Update` reached the main menu, but it advances on any button and the F9 probes ran first, so this run didn't show that Space did it. The plugin has since held the start screen back until the Space probe, which hasn't run on Linux yet. |
| Harmony prefixes on `Input.GetKey(KeyCode)` / `GetKeyDown(KeyCode)` (synthetic LeftAlt + PageUp) | `PlayerManager.Update` toggled `showCollisionValues`, so game code calls the patched methods and doesn't inline them. |
| Focus | The game window was in the foreground at the start screen without any help. |

What follows from this:
- **Tier 3 (OS input) is viable on Proton**, so it's very likely viable on Windows too, where `SendInput` is the native path. Windows itself is still to be checked.
- **Tier 2 legacy `Input` patching is viable.**
- Rewired action injection (risk 3) now matters only for deterministic in-process tests, not for reaching gameplay at all.
- **The game needs a display.** With the monitor off, Unity logs `MonitorFromWindow failed: Success.` in `Player.log` and either stalls before the first scene, so BepInEx never loads plugins, or loads them and takes longer than the spike's 90 s wait to reach the start screen. The runner should look for that line in `Player.log` (`AppData/LocalLow/Steelpan Interactive/Zeepkist/`) and fail fast with "no display", rather than time out.

### Running it on Windows (#2)

This takes the .NET 10 SDK, and Steam running with Zeepkist installed. It makes two runs, from the repo root:

```powershell
dotnet build spikes\InputAndYaml\Zeepkist.Tester.Spike.csproj -c Release

# The game in front: leave the mouse and keyboard alone until the script ends
powershell -ExecutionPolicy Bypass -File spikes\InputAndYaml\run.ps1 $HOME\spike-out\front

# The game behind another window: start this with an empty Notepad focused and keep Notepad in front until the script ends
powershell -ExecutionPolicy Bypass -File spikes\InputAndYaml\run.ps1 $HOME\spike-out\behind
```

For the second run, note whether the game's window comes to the front by itself when it appears. If it does, go back to Notepad by clicking it, or with Alt+Tab if the game is full screen. Key presses can land in Notepad, which is why it should be empty. The game has `runInBackground` on, so it should keep going behind Notepad.

In `results.json` of the second run:
- `focus.game_window_is_foreground` has to be `false`. If it's `true`, `SetForegroundWindow` was never called, so run it again with a new folder and get back to Notepad sooner.
- `focus.set_foreground_returned` and `focus.game_window_is_foreground_after` answer whether `SetForegroundWindow` works from inside the game.
- Each input probe has its own `game_window_is_foreground`, which says whether it ran in front or behind.

Each folder gets `LogOutput.log` and `Player.log`, plus `results.json` unless the game never got as far as the plugin. A `LogOutput.log` without this run's id is left over from an earlier run. When a probe gets stuck, `results.json` has what ran before it and the error under `exception`. These files, the script's output and whether the game came to the front by itself are what the Windows column comes from.

## 6. Decided

- **Windows is the main platform.** Most players and modders are on Windows, so the runner, the input drivers and the docs treat Windows as the main case and Linux/Proton as the second.
- **Scenarios are YAML** (decided 2026-11-04), in Maestro's shape with a JSON Schema generated from a typed step registry. See [scenario-format.md](scenario-format.md).

## 7. Scenario format

A scenario is a list of steps that run one after another. Many steps wait (seconds, frames, until something holds). There are assertions, a few multi-line text blocks (a Script's text), and sometimes a value from an earlier step (a placed block's UID). Loops and branches are rare but do come up (try each of 5 window heights). The step list differs per mod, so the format needs an editor that knows which steps exist.

| Format | Waits | Logic / values | Editor support | Rebuild to change? | Cost |
| --- | --- | --- | --- | --- | --- |
| Line DSL (today) | built into each step | none (`{uid:x}` substitution only) | none | no | the parser is ours; easy for agents to write |
| YAML / JSON | built into each step | ends up as expressions inside strings (`if: "${{ … }}"`), which reinvents a language | JSON Schema generated from the step registry → completion and checking in VS Code / Rider / VS | no | YamlDotNet in game; steps stay data |
| Lua (MoonSharp) | `test.wait(2)` is an ordinary call that yields | real variables, loops, functions; read values instead of only checking them | LuaLS `---@meta` file generated from the step registry → completion, hover docs, type checking | no | needs its own coroutine runner (§3 option A); can't `wait` inside `pcall` |
| C# `IEnumerator` tests | `yield return` | full C#, debugger | full IDE, compile-time errors | **yes** | none extra: the mod author's own language |

> **Superseded by [scenario-format.md](scenario-format.md):** a typed step registry, then YAML scenarios in Maestro's shape with a generated JSON Schema. Lua isn't needed. The tradeoff behind it stands: logic beyond literal loops and named values goes in C# tests, not in an expression language inside the format.

Recommendation: **C# tests as the base, a data format for scenarios that change without a rebuild, one step registry behind both.**

- **C# tests** are the base, like Unity Test Framework's `[UnityTest]`. An `[InGameTest] IEnumerator` method in the mod's test plugin gets the same `IStepContext` and input API. Mod authors already write C#, they already have a test plugin for their steps, and this is the one option where a refactor of the mod fails at compile time instead of in the game.
- **Scenarios** that change without a rebuild: an agent's repro for a ticket in `.scratch`, a quick check. The format is compared in scenario-format.md.
- **One step registry feeds everything:** C# calls, the scenario parser, the editor schema and the docs.

## 8. Windows

What changes from the bash runner:
- **Finding the game:** `HKCU\Software\Valve\Steam\SteamPath` → `steamapps/libraryfolders.vdf` → `appmanifest_1440670.acf`, the same lookup as now. `ZEEPKIST_DIR` stays as an override.
- **Game data:** `%APPDATA%\Zeepkist` (on Linux, the same folder inside the Proton prefix). Mod data, `Autosaves` and `Backups` are backed up from there.
- **Launch:** `steam.exe -applaunch 1440670 -ztest <runId>`. Detect the game by the `Zeepkist.exe` process. Steam's launch failures are in `<Steam>\logs\console_log.txt` on both platforms.
- **Lock between runs:** a lock file under `%LOCALAPPDATA%`, or a named mutex, instead of `flock`.
- **OS input:** `SendInput` works natively and is the reference platform for the OS driver. Windows doesn't let a background process steal focus (`SetForegroundWindow` restrictions), so the in-game driver brings its own window forward before sending. It also fails the step, saying why, when `GetForegroundWindow()` isn't the game's window, rather than typing into whatever has focus.
- **Proton** is the second platform: the same code, with the prefix path and whatever the `SendInput`-under-Wine spike finds.

## 9. Getting it to mod authors

How they run it decides how it ships. The simplest flow for a modder who already builds with the .NET SDK, ZeepSDK and Zeepkist.GameLibs from NuGet:

```sh
dotnet tool install Zeepkist.Tester.Cli            # once per repo (local tool manifest, version pinned)
dotnet ztest                                       # build, deploy, run every scenario and [InGameTest]
dotnet ztest Scenarios/window.lua --keep
```

That means two packages released together with the same version:

- **`Zeepkist.Tester` (NuGet), referenced by the mod's test project.** It carries:
  - the SDK assembly for compiling against (`ITestStep`, `[InGameTest]`, input API);
  - the in-game plugin DLL as package content, so the version a mod builds against is the version a run deploys;
  - MSBuild props that mark the project as a test plugin, so the CLI finds it.
- **`Zeepkist.Tester.Cli` (dotnet tool):** the runner.

The in-game plugin never goes on mod.io. The runner deploys it for a run and removes it afterwards, as today. Players never install it, and a mod's test plugin is never shipped with the mod.

Later, a VSTest adapter could add `dotnet test` support, so Rider and Visual Studio list the scenarios in their test explorers. That's worth it once the format and API have settled, not before.

## 10. Open questions

- Option A now with B later, or go straight to the ZeepSDK change?
- Should the CLI also be able to install a specific ZeepSDK build for a run, so a ZeepSDK change can be tested against the mods that use it?
