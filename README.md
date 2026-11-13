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

`ztest check` finds mistakes in scenarios with no game: unknown steps and arguments, values that don't fit, names used before they're defined and missing `run:` files as errors, and steps whose required state doesn't hold as warnings. It prints `file:line:col: error|warning: message` and exits with 1 on errors.

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

`ztest check` rewrites the schema each time it runs under a `zeepkist-test.json`, so it keeps up with your steps. It's generated, so add `.ztest/` to `.gitignore`. A scenario can also name the schema itself, relative to its own file:

```yaml
# yaml-language-server: $schema=../.ztest/schema.json
```

| Editor | How it finds the schema | What works |
| --- | --- | --- |
| VS Code, with Red Hat's [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) | The `yaml.schemas` entry `ztest init` writes, or the modeline above | Not checked by hand yet. The schema is checked against this repo's in-game scenarios with ajv and the `yaml` package that extension uses. |
| Rider | A `# $schema: ../.ztest/schema.json` comment at the top, or a mapping under *Settings → Languages & Frameworks → Schemas and DTDs → JSON Schema Mappings* | Not checked by hand yet. |
| Visual Studio 2022 | Unverified: schema-driven YAML support from 17.1 is reported, for `.yaml` files only | Not checked by hand yet. |

The schema is lenient where an editor reads YAML differently from the tester: a text value may be a number, and a mod's step may be written without its prefix whether or not `use:` lists its namespace. `ztest check` catches what it lets through.

## License

[MIT](LICENSE)
