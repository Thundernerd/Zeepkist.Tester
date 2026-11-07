# Run files

What the runner and the harness in the game (`Zeepkist.Tester`) hand each other. Background: [generic-tester.md](../research/generic-tester.md) §1.

## `run.json`

The runner deploys it next to the plugin. The plugin does nothing unless the game also started with `-ztest <runId>` and the id matches, so a `run.json` left behind by a crashed run can't start tests in normal play. One that can't be read counts as no run.

```json
{
  "runId": "20261105-120000",
  "mods": [
    { "guid": "Zeepkist.LogicScripting", "oldGuids": ["net.tnrd.zeepkist.logicscripting"] }
  ],
  "scenarios": ["window.ztest.yaml", "compile/errors.ztest.yaml"]
}
```

Each mod in `mods` has to pass the build check, as a step in the results named `right build: <guid>`:
- It's loaded from the folder named after its GUID next to the plugin's own folder, where the runner deploys it.
- No other plugin with its name or one of its `oldGuids` is loaded.
- BepInEx's log has no `Skipping [<name> <version>]`, which is how it reports a copy it skipped.

After the build check, a run has a `yaml parser` step. It parses and binds a small scenario, and fails unless that worked and YamlDotNet is the copy merged into the plugin. Its `Detail` says which assembly YamlDotNet came from.

Then each file in `scenarios` runs, in order, from the `scenarios` folder next to the plugin, unless one of those checks failed. See *Running scenarios* in [step-registry.md](step-registry.md). A run with no `scenarios` ends with a failed step named `scenarios`, so it can't pass.

## `out/results.json`

In the `out` folder next to the plugin, rewritten after every step, so a crash still leaves the steps before it. It's written to a temporary file and swapped in, so a reader never sees half a file. A reader should open it sharing read, write and delete, or the swap can fail while it's open.

| Field | What it is |
| --- | --- |
| `RunId` | The run's id. |
| `Done` | The run finished and the game is about to quit. |
| `Passed` | There are steps, and they all passed. Only meaningful with `Done`. |
| `Steps` | Each with `Name`, `Passed`, `Detail`, `Scenario`, `File`, `Line`, `RanFrom`, `Passes`, `Expected`, `Actual`, `Seconds`. `Expected` and `Actual` are null except on checks. Steps outside a scenario, and the walks to where each one starts, have an empty `Scenario` and `File` and a `Line` of 0. `Scenario` is the file name without `.ztest.yaml`. `File` and `Line` are where the step is written, and `RanFrom` is each `run:` that led there, as `file:line` outermost first, joined by ` > `. `Passes` says which pass of the `for`s and `repeat`s around it, such as `h=64, repeat 2/3`. |
| `Screenshots` | File names, in `out` too, each `<scenario>-<name>.png`. A failed step or start takes `<scenario>-failure.png`, and a failure outside a scenario takes `failure.png`. |
| `BlankScreenshots` | File name to color, for those where every pixel is the same color. |
| `Display` | The game's resolution, window mode and screen size, as the run ended. |
| `Exceptions` | Each with `Scenario`, `Message`, `Frame` (the first stack line) and `Count`, grouped by message and frame per scenario. They don't fail the run. |

## The log

The plugin's lines in BepInEx's `LogOutput.log` start with `[ZTEST]`. The last before quitting is `[ZTEST] run <runId> passed` or `failed`, and the log is flushed right after it, so the runner can read what comes after that line as the exceptions at quit.
