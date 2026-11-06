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
| `Zeepkist.Tester.Tests.SampleMod` | A mod's assembly on net472, for the unit tests to build descriptors from |
| `Zeepkist.Tester.Tests.SampleCore` | An assembly on net472 with a step as the tester ships them and a state for a mod's to nest in, for the same tests |

The build should finish with zero warnings. CI treats any warning as an error.

## License

[MIT](LICENSE)
