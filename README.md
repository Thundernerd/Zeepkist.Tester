# Zeepkist.Tester

An in-game test harness for [Zeepkist](https://store.steampowered.com/app/1440670/Zeepkist/) mods. It runs scenarios inside the real game: the game starts through Steam, walks into the level editor (or the main menu, or free play), runs each scenario's steps, and reports what passed, what failed and what it logged, with screenshots. Afterwards it puts the game install and your mods' data back as they were.

- **Scenarios** are YAML files, with completion and checking in VS Code, Rider and Visual Studio from a generated schema (see [Editor support](#editor-support)).
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

`ztest check` finds mistakes in scenarios with no game: unknown steps and arguments, values that don't fit, names used before they're defined and missing `run:` files as errors, and steps whose required state doesn't hold as warnings. It prints `file:line:col: error|warning: message` and exits with 1 on errors. It reports every problem it can in one run, such as each value that doesn't fit, and only stops at the first in a file that isn't valid YAML. Names and states are only checked in a file with no other errors.

```sh
ztest check Scenarios/window.ztest.yaml --plugin bin/Debug/net472/MyMod.InGameTests.dll
ztest check                # the "scenarios" folder and "testPlugin" of the nearest zeepkist-test.json
ztest check --registry     # states no step enters, and other smells in the steps and states themselves
```

## Editor support

Scenarios get completion, checking and hover docs from a JSON Schema of the tester's steps and your test plugin's:

```sh
ztest schema    # writes .ztest/schema.json next to the nearest zeepkist-test.json
ztest init      # has VS Code check **/*.ztest.yaml against it, in .vscode/settings.json
```

`ztest check` rewrites the schema each time it reads the steps under a `zeepkist-test.json`, so checking keeps it up with your steps. `ztest init` writes `.vscode/settings.json` next to `zeepkist-test.json`, and VS Code only reads it from the folder it has open, so keep `zeepkist-test.json` at the root of your repo, or move the entry to the root's settings with the path from there. It's generated, so add `.ztest/` to `.gitignore`. A scenario can also name the schema itself, relative to its own file:

```yaml
# yaml-language-server: $schema=../.ztest/schema.json
```

| Editor | How it finds the schema | What works |
| --- | --- | --- |
| VS Code, with Red Hat's [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) | The `yaml.schemas` entry `ztest init` writes, or the modeline above | Not checked by hand yet. The schema is checked against this repo's in-game scenarios with ajv and the `yaml` package that extension uses. |
| Rider | A `# $schema: ../.ztest/schema.json` comment at the top, or a mapping under *Settings → Languages & Frameworks → Schemas and DTDs → JSON Schema Mappings* | Not checked by hand yet. |
| Visual Studio 2022 | Unverified: schema-driven YAML support from 17.1 is reported, for `.yaml` files only | Not checked by hand yet. |

The schema is lenient where an editor reads YAML differently from the tester: a text value may be a number or `null`, a number may be quoted, and a mod's step may be written without its prefix whether or not `use:` lists its namespace. `ztest check` catches what it lets through.

## Step reference

`ztest docs` writes every step and state, the tester's and your mod's, as Markdown: each step's summary, arguments, the states it needs and changes, and its examples, and for each state the steps that get into it and out of it. Link to the file rather than keeping a table of steps by hand.

```sh
ztest docs --plugin bin/Debug/net472/MyMod.InGameTests.dll   # writes steps.md
ztest docs --output docs/steps.md                             # the "testPlugin" of the nearest zeepkist-test.json
```

## Converting `.zlstest` files

`ztest convert` writes each of ZeepLogicScript's old `.zlstest` files as a `.ztest.yaml` next to it, and leaves the old file where it is. It won't write over a `.ztest.yaml` that's already there.

```sh
ztest convert Scenarios --plugin bin/Debug/net472/MyMod.InGameTests.dll
```

Each line becomes a step in its shortest YAML form, read by the step's `[LineSyntax("<function> [as <as>]")]`: `<name>` is one word of an argument, `<name...>` one or more, `[ … ]` can be left out, and other words are written as they are. A step without one takes the rest of the line as its primary argument. `set-text … end` becomes a `set-text: |-` block, `drag-window-edge … then …` a list of moves (`drag-window-edge-sideways` is `sideways: true`), `expect-error <line>:<column> <text>` a `{ line, column, message }` map, and `{uid:x}` in a `set-text` a `${x}`, as ZeepLogicScript only filled it in there. Comments stay where they were, and a mod's steps are listed under `use:`, or written with their namespace where the tester has a step of that name too.

## License

[MIT](LICENSE)
