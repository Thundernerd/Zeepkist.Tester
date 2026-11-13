# Zeepkist.Tester

An in-game test harness for [Zeepkist](https://store.steampowered.com/app/1440670/Zeepkist/) mods. It runs scenarios inside the real game: the game starts through Steam, walks into the level editor (or the main menu, or free play), runs each scenario's steps, and reports what passed, what failed and what it logged, with screenshots. Afterwards it puts the game install and your mods' data back as they were.

- **Scenarios** are YAML files, with completion and checking in VS Code, Rider and Visual Studio from a generated schema.
- **C# tests** (`[InGameTest]`) are for tests that need real logic.
- **Steps** come from the tester itself (wait, screenshot, press, click, test mode, …) and from your mod, which registers its own in C#.
- **Input** can be in-process and repeatable, or real OS input, the way a player's keyboard and mouse reach the game.

Status: in design. See the [issues](https://github.com/Thundernerd/Zeepkist.Tester/issues) for the plan.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), on Windows or Linux. The game doesn't have to be installed.

```sh
dotnet build
dotnet test
```

| Project | What it is |
| --- | --- |
| `Zeepkist.Tester.Sdk` | What mods compile against |
| `Zeepkist.Tester` | The BepInEx plugin: the harness in the game. What to deploy, with the parser and YamlDotNet merged in, is in `bin/<configuration>/net472/plugin/` |
| `Zeepkist.Tester.Scenarios` | The scenario parser, which the plugin and the CLI share |
| `Zeepkist.Tester.Cli` | The `ztest` dotnet tool: the runner and offline tools |
| `Zeepkist.Tester.Tests` | Unit tests |
| `Zeepkist.Tester.Tests.InGame` | A test plugin and scenarios for checking the harness in the real game. `run.sh <out dir>` deploys them, runs the game through Steam, compares the results with `expected-results.txt` and puts everything back. Four scenarios and two C# tests fail on purpose. `run.sh <out dir> --start-states` walks to each start state from the editor, test mode and free play, against `expected-start-states.txt` |
| `Zeepkist.Tester.Tests.SampleMod` | A mod's assembly on net472, for the unit tests to build descriptors from |
| `Zeepkist.Tester.Tests.SampleCore` | An assembly on net472 with a step as the tester ships them and a state for a mod's to nest in, for the same tests |

The build should finish with zero warnings. CI treats any warning as an error.

## Checking scenarios

`ztest check` finds mistakes in scenarios with no game: unknown steps and arguments, values that don't fit, names used before they're defined and missing `run:` files as errors, and steps whose required state doesn't hold as warnings. It prints `file:line:col: error|warning: message` and exits with 1 on errors.

```sh
ztest check Scenarios/window.ztest.yaml --plugin bin/Debug/net472/MyMod.InGameTests.dll
ztest check                # the "scenarios" folder and "testPlugin" of the nearest zeepkist-test.json
ztest check --registry     # states no step enters, and other smells in the steps and states themselves
```

## Converting `.zlstest` files

`ztest convert` writes each of ZeepLogicScript's old `.zlstest` files as a `.ztest.yaml` next to it, and leaves the old file where it is. It won't write over a `.ztest.yaml` that's already there.

```sh
ztest convert Scenarios --plugin bin/Debug/net472/MyMod.InGameTests.dll
```

Each line becomes a step in its shortest YAML form, read by the step's `[LineSyntax("<function> [as <as>]")]`: `<name>` is one word of an argument, `<name...>` one or more, `[ … ]` can be left out, and other words are written as they are. A step without one takes the rest of the line as its primary argument. `set-text … end` becomes a `set-text: |-` block, `drag-window-edge … then …` a list of moves (`drag-window-edge-sideways` is `sideways: true`), `expect-error <line>:<column> <text>` a `{ line, column, message }` map, and `{uid:x}` a `${x}`. Comments stay where they were, and a mod's steps are listed under `use:`, or written with their namespace where the tester has a step of that name too.

## License

[MIT](LICENSE)
