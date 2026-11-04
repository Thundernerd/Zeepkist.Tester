# Scenario format for the generic tester

Which file format should Scenarios use once the tester is generic (see [generic-tester.md](generic-tester.md) §1, §2 and §7)? This compares the current line DSL (and ways to make it more workable), YAML, JSON/JSONC/JSON5, Maestro, Gherkin, Robot Framework, KDL v2, and a few others. Each format gets the same real scenario, translated.

Researched 2026-11-04. Local sources are `ZeepLogicScript` (`docs/ingame-tests.md`, `Zeepkist.LogicScripting.InGameTests/{ScenarioParser,StepArgument,StepList}.cs`, `Scenarios/*.zlstest`, `.scratch/ingame/*.zlstest`) and `Zeepkist.GameLibs/package/lib`. Web sources are cited inline. Anything marked **unverified** was not checked against a primary source or by running it.

## Summary

**Decided 2026-11-04: YAML.** **Recommendation: build a typed step registry first, then use YAML with a JSON Schema generated from it as the scenario format, in Maestro's shape.** Keep the line DSL readable through a one-off converter, not as a second format.

- The format matters less than the **step registry**. Today each step parses its own argument string (`StepArgument.Text/Count/Choice/...` in `StepList.cs`), so nothing outside the parser knows what a step takes. Every option below that has editor support needs the same thing: a machine-readable description of each step (its name, arguments, types, enums and docs) that each mod registers in C#. That registry then produces the parser's checks, the reference docs, and the editor schema or grammar.
- **YAML + generated JSON Schema** gets completion, validation and hover docs in VS Code (Red Hat YAML), Rider and Visual Studio, and the harness writes no editor tooling for it. The schema is generated per mod from the merged registry, so each mod gets an editor that knows its own steps. Maestro, the closest precedent, has done exactly this: its flows are YAML lists of single-key commands with a schema in SchemaStore. YamlDotNet ships a `net47` build with no dependencies and gives a line and column for every node, so errors still point at a line. `|` block scalars handle `set-text` better than `... end`.
- The YAML pitfalls are real but can be contained. The harness reads scalars as strings through the representation model (so there's no Norway problem in the game). The editor uses YAML 1.2 by default. The quoting rules, such as a value that starts with `{` or contains `: `, are caught by the schema in the editor before a run.
- **Runners-up:**
  - The *improved line DSL* is the tersest option and the easiest for agents. Its editor support costs a language server we'd write and maintain ourselves, roughly a small C# LSP plus a VS Code extension and an LSP4IJ entry for Rider.
  - *Gherkin with `*` steps* is a standard parser for almost the same line format, with doc strings, tables and outlines. It runs on netstandard2.0 and tracks line numbers, but there's no schema-driven completion for a custom step registry.
  - *KDL v2* looks most like today's format and has real nesting. Its only C# v2 parser targets netstandard2.1, and it has no schema-driven completion in any editor.
- Real logic (reading values, branching on game state) belongs in the C# `[InGameTest]` tier of generic-tester.md §7, not in an expression language inside YAML strings. The format only gets data-level control: `repeat`/`for` over literal values, sub-scenarios, and `as:` names with `${name}` substitution.

## What a Scenario needs

These come from the spec (`docs/ingame-tests.md`, "Scenarios" and "Steps"), the parser and about 80 real files:

- **Sequential steps**, many of which wait (`wait 2`, `test-mode` waits for the round, `complete` waits for rows).
- **Assertions with expected and actual values** (`context.Fail(detail, expected, actual)`).
- **Multi-line text:** `set-text` followed by lines up to `end`. A Function's own `end` has to be indented so it isn't read as the terminator (`ScenarioParser.ReadSetText`). Several Scenarios set the text 3–4 times (`216-setting-constants.zlstest`).
- **Named values from earlier steps:** `place-block lamp as lamp`, then `{uid:lamp}` in text, or `set-block-setting lamp …`.
- **Occasional repetition:** `214-window-min-height.zlstest` repeats `set-window-height` / `expect-footer-in-view` / `screenshot` for different states by copy-paste.
- **Per-line errors:** "A Scenario that can't be read fails before the game starts it, with the line that's wrong." A parse error is `line N: …` (`ScenarioParser.Parse`).
- **Per-mod step lists** (`TesterApi.RegisterStep(namespace, name, factory)` in generic-tester.md §4).
- **Authors:** people in Rider, VS or VS Code on Windows, and AI coding agents. Agents need a format they write correctly on the first try and a fast offline check (`ztest check`) that reports the line.

### The reference scenario

`.scratch/ingame/uid-showcase.zlstest` uses two `set-text` blocks (one with a nested Function `end`), a named block with `{uid:…}`, waits and screenshots:

```
# The showcase's Script: does placed(...) still find the lamp after test mode, once the window has rechecked?
open-window
new-library timingx
set-text
    function edge(x) -> rising
        prev = delay(x, ticks: 1)
        rising = x & !prev
    end
end
new-script Lights
place-block lamp as lamp
set-text
    p = player_input()
    t = simple_telemetry()
    power_display(t.speed)
    toggler(timingx.edge(p.horn), targets: placed("{uid:lamp}"))
end
expect-no-errors
compile
expect-result Placed
place-start
test-mode
wait 2
back-to-editor
open-window
select-script Lights
screenshot right-away
wait 3
screenshot after-wait
expect-no-errors
```

Each format below translates this file, and then a loop: "for each of 64, 120, 240 and 640 px: `set-window-height`, `expect-footer-in-view`, `screenshot`".

## Comparison

| Format | Multi-line text | Named values | Loops / reuse | Errors with line | Editor completion for per-mod steps | Parser on net472 / Unity Mono | Agents |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Line DSL (today) | `set-text … end`, an inner `end` must be indented | `as x` + `{uid:x}`, special-cased per step | none | yes (own parser) | none | own code | very good: terse, no quoting |
| Line DSL, improved | same, or a `"""` fence | generic `as` + `${x}` | `for … end`, `run file` | yes | **build it**: steps.json → LSP or VS Code extension | own code | very good |
| **YAML + JSON Schema** | `\|` block scalar, clean | `as:` property + `${x}` | `repeat` / `for` / `run` nodes | yes, `Mark` on every node | **free**: Red Hat YAML, Rider, VS via a schema we generate | YamlDotNet 18.1.0 `net47`, MIT, no deps | good: agents know YAML, the schema catches mistakes |
| JSON / JSONC | arrays of lines or `\n` escapes, poor | `as` property | as YAML | yes, `IJsonLineInfo` | free: VS Code / Rider / VS via `$schema` | **Newtonsoft 13.0.2 already in the game** | fair: noisy, script text is painful |
| JSON5 | `\` line continuation only | as JSON | as JSON | n/a | weak | NuGet `Json5` is net8+/net10 only | fair |
| Maestro (as precedent) | `inputText` strings | `${output.x}` via JS | `repeat`, `runFlow`, `when`, JS | n/a (JVM) | schema in SchemaStore | not reusable (Kotlin) | good |
| Gherkin (`*` steps) | `"""` doc strings, dedented | via step context | Scenario Outline + Examples, Background | yes, `Location` | Cucumber extension finds bindings in C# source; no registry-driven completion | Gherkin 42.0.1, netstandard2.0, MIT | good |
| Robot Framework | awkward (`...` cells, `${SPACE}`) | `${x}=  Keyword` | FOR/WHILE/IF, user keywords | n/a (Python) | RobotCode LSP (Python libs only) | not reusable | fair |
| KDL v2 | `"""` with dedent, raw `#"""…"""#` | `as=x` property | children blocks `for … { }` | yes, `SourcePosition` (KdlSharp) | syntax highlighting only | KdlSharp 1.1.0 is **netstandard2.1**, v2 only there | fair: less familiar, `#true` and `//` trip people |

## 1. The current line DSL, and making it more workable

### What it is

One step per line, `name rest-of-line`. Blank lines and `#` comments are skipped. `set-text` takes the lines up to a line that is just `end`, and removes their shared indent (`ScenarioParser.cs`). `StepList` is a static `Dictionary<string, Func<string,string,ScenarioStep>>` where each entry parses its argument string with `StepArgument` helpers: `Text`, `Count`, `Choice`, `Pixels`, `Size`, `Seconds`, `Keys`, `FileName`. These helpers throw `ScenarioFormatException("place-block takes …, not 'x'")`, which the parser turns into `line N: …`.

What works: it's very terse, it reads like a transcript, nothing needs quoting, and agents write it reliably (the `.scratch/ingame/` folder holds about 60 agent-written files). Errors come per line.

What doesn't:
- Argument shapes live only in code, so nothing outside the parser can check or complete them.
- `end` inside script text collides with the terminator.
- There's no loop, so `214-window-min-height` repeats itself.
- `{uid:x}` is a special case for one step.
- No editor support at all (generic-tester.md §7).

### Concrete improvements

1. **Typed argument specs per step.** Replace `Func<string,string,ScenarioStep>` with a declarative spec that the SDK exposes to mods. For example:

   ```csharp
   steps.Add("place-block", "Places a block of the Block Function's type beside the start block, and selects it.")
        .Arg("function", ArgType.Word, "A Block Function's name, such as lamp")
        .Option("as", ArgType.Name, "Names the block for ${name} and set-block-setting")
        .Create(a => new PlaceBlockStep(a.Word("function"), a.NameOrNull("as")));
   steps.Add("show-tab", "…").Arg("tab", ArgType.Choice("scripts", "libraries")).Create(…);
   steps.Add("set-text", "…").Arg("text", ArgType.Block).Create(…);
   ```

   The same spec parses the line, so the `StepArgument` messages stay. It also produces the docs table in `ingame-tests.md`, which `StepListTests` checks by hand today. The same spec can produce the JSON Schema for YAML (§2) or the grammar for KDL (§7). **This is format-independent and should be done whichever format wins.**
2. **Generic named values.** `as <name>` becomes a registry feature: a step declares that it *produces* a value, such as a block UID. A `${name}` (or today's `{uid:name}`) in any text argument is then substituted, with an error on the line when the name is unknown.
3. **Blocks.** Use a fence that can't collide with script text, for example `set-text """` … `"""` (as in Gherkin and KDL), and keep `end` only for control blocks. Add `for h in 64 120 240 640` … `end` and `run other.zlstest` for reuse.
4. **Generated `steps.json`.** The CLI (`zeepkist-test`, host side, modern .NET, so no net472 limit) loads the mod's test plugin and the tester's core steps, and writes every spec to `steps.json`. Each entry has the name, namespace, argument list with types and choices, docs, and the source file.
5. **Editor support, driven by `steps.json`:**
   - **TextMate grammar.** It's fixed and generic: the first word is a step, `#` starts a comment, `${…}` is a substitution, and a fenced block is embedded text. VS Code tokenizes with TextMate grammars contributed through `contributes.grammars` ([VS Code syntax highlight guide](https://code.visualstudio.com/api/language-extensions/syntax-highlight-guide)). Rider can import TextMate bundles (**unverified** for this case).
   - **Completion and diagnostics.** These need a language server that reads `steps.json`: complete step names, then choices such as `scripts|libraries`, then names defined earlier with `as`. It reports parse errors by running the same C# parser. A C# server can use [OmniSharp's C# LSP library](https://github.com/OmniSharp/csharp-language-server-protocol) ("an implementation of the Language Server Protocol written entirely in C# for .NET"). It ships as part of the CLI (`ztest lsp`), so the parser is the same code the game runs. Clients: a thin VS Code extension; Rider through Red Hat's [LSP4IJ](https://github.com/redhat-developer/lsp4ij), an "LSP client compatible with all flavours of IntelliJ" that also takes user-defined servers; Visual Studio through an LSP extension (**unverified** effort).
   - **Cost:** a language server, a VS Code extension published to the marketplace, and LSP4IJ setup docs. All of it is ours to maintain. The YAML route gets the same features from a generated schema file.
6. **Precedent for keyword DSLs with editor support:**
   - [RobotCode](https://github.com/robotcodedev/robotcode) is "a language server, IDE extensions for VS Code and the JetBrains platform", with "completion for keywords, variables, libraries". It gets keyword metadata from Robot's Libdoc, which can write [JSON spec files](https://robotframework.org/robotframework/latest/RobotFrameworkUserGuide.html#libdoc-json-spec-files). That's the same idea as `steps.json`.
   - The [Cucumber VS Code extension](https://github.com/cucumber/vscode) is "powered by the Cucumber Language Server" and autocompletes steps from step definitions, C#/SpecFlow included.
   - [Hurl](https://hurl.dev/docs/hurl-file.html) is a custom plain-text format (`#` comments, `[Asserts]`/`[Captures]` sections, `{{var}}` from captures) that is pleasant without being YAML.

### The reference scenario, improved line DSL

```
# The showcase's Script: does placed(...) still find the lamp after test mode, once the window has rechecked?
open-window
new-library timingx
set-text """
    function edge(x) -> rising
        prev = delay(x, ticks: 1)
        rising = x & !prev
    end
    """
new-script Lights
place-block lamp as lamp
set-text """
    p = player_input()
    t = simple_telemetry()
    power_display(t.speed)
    toggler(timingx.edge(p.horn), targets: placed("${lamp}"))
    """
expect-no-errors
compile
expect-result Placed
place-start
test-mode
wait 2
back-to-editor
open-window
select-script Lights
screenshot right-away
wait 3
screenshot after-wait
expect-no-errors
```

Loop:

```
for h in 64 120 240 640
    set-window-height ${h}
    expect-footer-in-view
    screenshot scripts-${h}
end
```

## 2. YAML

### What it looks like

In Maestro's shape: a list of single-key maps, where a step with one argument takes a scalar and a step with several takes a map. A step without arguments is a bare string.

```yaml
# yaml-language-server: $schema=../.ztest/steps.schema.json
description: "The showcase's Script: does placed(...) still find the lamp after test mode, once the window has rechecked?"
steps:
  - open-window
  - new-library: timingx
  - set-text: |
      function edge(x) -> rising
          prev = delay(x, ticks: 1)
          rising = x & !prev
      end
  - new-script: Lights
  - place-block: { function: lamp, as: lamp }
  - set-text: |
      p = player_input()
      t = simple_telemetry()
      power_display(t.speed)
      toggler(timingx.edge(p.horn), targets: placed("${lamp}"))
  - expect-no-errors
  - compile
  - expect-result: Placed
  - place-start
  - test-mode
  - wait: 2
  - back-to-editor
  - open-window
  - select-script: Lights
  - screenshot: right-away
  - wait: 3
  - screenshot: after-wait
  - expect-no-errors
```

Loop and reuse, as data with no expressions:

```yaml
  - for: { h: [64, 120, 240, 640] }
    steps:
      - set-window-height: ${h}
      - expect-footer-in-view
      - screenshot: scripts-${h}
  - run: shared/open-lights.yaml
```

Multi-line text is YAML's strong point. A `|` literal block keeps newlines, the indentation is removed for you, and a Function's `end` needs no special care. A mod's steps would be namespaced (`zls.open-window`) if names collide. Whether core steps stay unprefixed is a registry decision.

### Extending steps

The registry from §1 produces a JSON Schema. `steps` is an array whose items are `oneOf`, one subschema per step: `"open-window"` as a `const` string, or `{ "new-library": { "type": "string" } }` with `additionalProperties: false`. Choices become `enum`, and docs become `description`/`markdownDescription`. Each mod's CLI run writes the schema for *its* registry (core plus that mod's steps), so completion matches the mod. The harness validates the same rules at load time, from the same spec, and reports the YAML node's line.

### Parser: YamlDotNet

- [YamlDotNet on NuGet](https://www.nuget.org/packages/YamlDotNet/): 18.1.0, published 2026-06-26, license MIT, targets `net10.0; net8.0; netstandard2.0; netstandard2.1; net47`, **with no dependencies on any target** (NuGet registration metadata). A net472 project picks the `net47` build. The source `YamlDotNet.csproj` lists `<TargetFrameworks>net10.0;net8.0;netstandard2.0;netstandard2.1;net47</TargetFrameworks>` ([repo](https://github.com/aaubry/YamlDotNet)).
- Spec support: the parser handles YAML 1.1 and 1.2, and the emitter only 1.1 ([README](https://github.com/aaubry/YamlDotNet/blob/master/README.md)).
- Unity: "YamlDotNet is also available on the [Unity Asset Store](https://assetstore.unity.com/packages/tools/integration/yamldotnet-for-unity-36292)" (README). That it loads and runs in Zeepkist's Mono under BepInEx is **unverified**, but very likely for a pure managed net47 DLL.
- **Line numbers:** every parsing event and every `YamlNode` of the representation model has `Start`/`End` of type `Mark`, with `Line` and `Column`. `YamlException` also carries `Start`/`End` (`YamlDotNet/Core/Events/ParsingEvent.cs`, `RepresentationModel/YamlNode.cs`, `Core/YamlException.cs`, `Core/Mark.cs`). Load with `YamlStream`, walk the nodes, and report `line {node.Start.Line}: …`. The per-line errors are kept.
- Risk: BepInEx loads every plugin into one AppDomain, so another mod shipping a different YamlDotNet version could clash (**unverified** whether any Zeepkist mod ships YamlDotNet). One way out is to ILRepack or internalize it into the tester. A second risk is churn: YamlDotNet is very widely used (about 612M NuGet downloads) but has had many major versions (18.x in 2026), so pin a version.
- Alternatives, not recommended:
  - [VYaml](https://www.nuget.org/packages/VYaml/) 1.6.0 (MIT, 2026-11-01), built for Unity. On netstandard2.0 it needs System.Memory, System.Buffers and System.Threading.Tasks.Extensions.
  - [SharpYaml](https://www.nuget.org/packages/SharpYaml/) 3.16.0 (MIT). On netstandard2.0 it pulls System.Text.Json 10 and System.Collections.Immutable 9, which is heavy for the game.

### Editor support

- **VS Code** with [Red Hat YAML](https://github.com/redhat-developer/vscode-yaml): "Schemas can be associated with YAML files using a modeline, an inline `$schema` property, or the `yaml.schemas` setting". The modeline is `# yaml-language-server: $schema=<schema-url-or-path>`, and "Relative paths in modelines are resolved from the YAML file's location". It also matches files against SchemaStore automatically (`yaml.schemaStore.enable`, on by default). The server supports JSON Schema draft-04, draft-07, 2019-09 and 2020-12 ([yaml-language-server](https://github.com/redhat-developer/yaml-language-server)), and also reads the IntelliJ-style `# $schema: <path>` comment.
- **Rider:** "JetBrains Rider supports code completion and inspections for YAML files using the JSON schema", and you can put `# $schema:` at the top of the file ([Rider YAML help](https://www.jetbrains.com/help/rider/YAML.html)). IntelliJ also loads SchemaStore schemas by file pattern ([IntelliJ YAML help](https://www.jetbrains.com/help/idea/yaml.html)).
- **Visual Studio 2022:** schema-driven YAML IntelliSense since 17.1, according to one [Microsoft Q&A answer](https://learn.microsoft.com/en-us/answers/questions/1627740/how-to-get-visual-studio-2022-to-edit-a-yaml-file). It only works for `.yaml` files. **Unverified** against official docs.
- **SchemaStore:** a catalog entry with a `fileMatch` such as `**/*.ztest.yaml` would associate the schema automatically, the way [Maestro Flow](https://www.schemastore.org/maestro-flow.json) does (`**/.maestro/**/*.yaml`, `**/*.flow.yaml`; [catalog](https://www.schemastore.org/api/json/catalog.json)). Per-mod steps can't live in one central schema, though, so a local generated schema plus a modeline (or a `yaml.schemas` entry the CLI writes into `.vscode/settings.json`) is the practical route.

### Pitfalls

- **Norway problem.** YAML 1.1 reads `no`, `on`, `off`, `y` and `yes` as booleans ([YAML 1.1 bool type](https://yaml.org/type/bool.html)), so `library-read-failed: on` could become `true`.
  - *In the editor:* Red Hat YAML defaults to 1.2 ("The default YAML spec version is `1.2`"), where `on` is a string, so it matches an `enum: [on, off]`.
  - *In the game:* the deserializer's bool regex is `^(true|y|yes|on)$` (`ScalarNodeDeserializer.cs`), but that only applies when deserializing *into* a `bool`. Reading the representation model keeps every scalar a string, and the step spec converts it.
  - So it's contained if the harness never uses typed deserialization.
- **Quoting.** A plain scalar can't start with `{`, `[`, `*`, `&`, `!`, `|`, `>`, `'`, `"`, `%` or `@`, and can't contain `: ` or ` #`. Real step arguments hit this:
  - `expect-text: {uid:lamp}` becomes a flow mapping;
  - `expect-hover: 2: x = 1` is invalid;
  - `type: (` is fine, but `type: [` is not;
  - `expect-completion: t logic` is fine.

  The schema flags the wrong type at once in the editor, and the harness reports "expected a string, found a map" with the line. Agents generally quote correctly when told "quote values with `:`, `#`, `{`".
- **Indentation.** A step indented one level too far becomes part of the previous map. The schema (`additionalProperties: false`) catches most of these.
- **Verbosity.** `- ` and `: ` on every line. It's about 10% more characters than the line DSL for the reference scenario.
- **Expressions creeping in.** Maestro needed JS for counters and conditions (§4). Our rule: control nodes take only literal lists and names, and anything else is a C# test.

## 3. JSON / JSONC / JSON5

```jsonc
{
  "$schema": "../.ztest/steps.schema.json",
  "description": "The showcase's Script: does placed(...) still find the lamp after test mode?",
  "steps": [
    "open-window",
    { "new-library": "timingx" },
    { "set-text": [
        "function edge(x) -> rising",
        "    prev = delay(x, ticks: 1)",
        "    rising = x & !prev",
        "end" ] },
    { "new-script": "Lights" },
    { "place-block": { "function": "lamp", "as": "lamp" } },
    { "set-text": [
        "p = player_input()",
        "t = simple_telemetry()",
        "power_display(t.speed)",
        "toggler(timingx.edge(p.horn), targets: placed(\"${lamp}\"))" ] },
    "expect-no-errors",
    "compile",
    { "expect-result": "Placed" },
    "place-start",
    "test-mode",
    { "wait": 2 },
    "back-to-editor",
    "open-window",
    { "select-script": "Lights" },
    { "screenshot": "right-away" },
    { "wait": 3 },
    { "screenshot": "after-wait" },
    "expect-no-errors"
  ]
}
```

Loops look like YAML's: `{ "for": { "h": [64, 120, 240, 640] }, "steps": [ … ] }`.

- **Parser already in the game.** `Zeepkist.GameLibs/package/lib/Newtonsoft.Json.dll` is Json.NET **13.0.2** (`13.0.2+cf0a246981c33ae00121dfe58b850aefeac1aac0`, assembly version 13.0.0.0). Its attributes say `.NETStandard,Version=v2.0` and the configuration `UnityAOT`, so it's Unity's Newtonsoft package build (checked with `monodis`/`strings` on the DLL). License MIT ([NuGet](https://www.nuget.org/packages/Newtonsoft.Json/), latest stable 13.0.4, 2025-09-16). There's no extra assembly and no version clash, since the tester uses the game's copy.
- **Line info.** `IJsonLineInfo` (`LineNumber`/`LinePosition`) is implemented by `JToken` and `JsonTextReader`, and `JsonReaderException` carries the line too. `JsonLoadSettings.LineInfoHandling` defaults to `Load` and `CommentHandling` defaults to `Ignore`, so `//` and `/* */` comments are tolerated ([JsonLoadSettings](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_Linq_JsonLoadSettings.htm), [IJsonLineInfo](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_IJsonLineInfo.htm)). All of these types are in the game's DLL (`monodis --typedef`). Trailing-comma tolerance is **unverified**.
- **Editor support.** VS Code reads a `$schema` key or the `json.schemas` setting, and "also has a JSON with Comments (jsonc) mode" that "accepts trailing commas, but they are discouraged". `defaultSnippets` and `markdownDescription` are VS Code-only schema extensions ([VS Code JSON docs](https://code.visualstudio.com/docs/languages/json)). Rider and VS both do JSON Schema completion as well. Editor support is as good as YAML's.
- **The problem is text.** JSON has no multi-line strings. Script text becomes an array of lines or `\n`-escaped strings with every `"` escaped. That's the most common thing these Scenarios do, and it's where agents and people both make mistakes.
- **JSON5** ([spec](https://spec.json5.org/)) adds comments, unquoted keys and trailing commas. Multi-line strings only come by escaping the newline with `\`, so the text problem stays. The NuGet [`Json5`](https://www.nuget.org/packages/Json5/) package (1.0.4, MIT) targets only net8.0/net10.0, so it doesn't run in the game.

Verdict: a good *machine* format for `results.json` and `run.json`, and a fallback if a YAML dependency is unacceptable. Too painful as the scenario format.

## 4. Maestro (closest precedent)

[Maestro](https://docs.maestro.dev/get-started/what-is-maestro.md) is "an open-source UI automation framework for mobile and web with built-in tolerance, zero-wait intelligence, and declarative YAML syntax" (Apache-2.0, [repo](https://github.com/mobile-dev-inc/Maestro)). A flow is YAML: an optional header (`appId`, `env`), then `---`, then a list of commands such as `- tapOn: Username`, `- inputText: Test User` and `- assertVisible: …` ([commands](https://docs.maestro.dev/api-reference/commands)). There are about 50 commands, including `extendedWaitUntil`, `takeScreenshot`, `pressKey`, `repeat`, `retry`, `runFlow`, `runScript`, `evalScript` and `assertTrue`.

How it handles the hard parts:
- **Sub-flows:** `runFlow` takes a `file`, a `label`, an `env` map for parameters, or inline `commands`. Inline sub-flows exist mostly to carry a `when` condition ([runFlow](https://docs.maestro.dev/reference/commands-available/runflow.md)).
- **Loops:** `repeat` takes `times`, `while` (a condition) and `commands`. Combining `times` with `while` gives a capped loop. Counters need JS: `- evalScript: ${output.counter = 0}` … `while: { true: ${output.counter < 3} }` ([repeat](https://docs.maestro.dev/reference/commands-available/repeat.md)).
- **Conditions:** `when:` with `visible`, `notVisible`, `platform` or `true` (a JS expression). Commands can also be `optional: true`. Maestro's own docs warn: "Overusing conditional logic can make your Flows hard to read and debug. Prefer separate Flows for significantly different scenarios." ([conditions](https://docs.maestro.dev/maestro-flows/flow-control-and-logic/conditions.md))
- **Variables:** `${NAME}` for `env` constants and `-e` CLI parameters, which are always strings ("you may need to parse it in JavaScript"; [parameters](https://docs.maestro.dev/maestro-flows/flow-control-and-logic/parameters-and-constants.md)). Values from earlier steps go through the JS `output` object.
- **JS escape hatch:** `runScript` (a file, with `env`) and `evalScript` (one line) run on GraalJS by default ("Rhino is also supported… Maestro discourage the Rhino usage"; [JS overview](https://docs.maestro.dev/maestro-flows/javascript/javascript-overview.md), [runScript](https://docs.maestro.dev/reference/commands-available/runscript.md), [evalScript](https://docs.maestro.dev/reference/commands-available/evalscript.md)).
- **Editor:** a "Maestro Flow" JSON Schema in SchemaStore, matched on `**/.maestro/**/*.yaml` and `**/*.flow.yaml` ([catalog](https://www.schemastore.org/api/json/catalog.json)).
- **Extending commands:** none. The command set is fixed in Maestro's Kotlin code, and the JS hooks are the only extension point (no plug-in commands found in the docs). This is where we must differ: our step list is per-mod, so our schema has to be generated.

What to copy: single-key command items with short and long forms, `runFlow`-style `run:` with parameters, `repeat` with a cap, `label:` for reports, `optional:`. What not to copy: `${…}` JS inside strings as the way to get logic. Maestro is the cautionary tale behind generic-tester.md's "reinvents a language" point. If an escape hatch is wanted, the analogue is a `lua:` step on the tester's own MoonSharp `Script` (generic-tester.md §3 option A). The C# test tier is better still.

Maestro itself can't be reused or forked. It's a JVM program that drives devices from outside, while our harness runs inside the game process.

The reference scenario in literal Maestro style would be the YAML of §2: `- wait: 2` maps to its `waitForAnimationToEnd`/`extendedWaitUntil`, and `- screenshot: x` to `takeScreenshot: x`.

## 5. Gherkin (Reqnroll, or the plain parser)

### What it looks like

Classic Given/When/Then:

```gherkin
Feature: Placed block references

  Scenario: The showcase's Script still finds the lamp after test mode, once the window has rechecked
    Given the Script window is open
    And a new Library "timingx" with the text:
      """
      function edge(x) -> rising
          prev = delay(x, ticks: 1)
          rising = x & !prev
      end
      """
    And a new Script "Lights"
    And a lamp block placed as "lamp"
    When the Script's text is:
      """
      p = player_input()
      t = simple_telemetry()
      power_display(t.speed)
      toggler(timingx.edge(p.horn), targets: placed("{uid:lamp}"))
      """
    Then the check finds no errors
    When I compile
    Then the result line contains "Placed"
    ...
```

Or keep today's step names with Gherkin's `*` keyword, which keeps almost exactly today's look:

```gherkin
Scenario: placed(...) still finds the lamp after test mode
  * open-window
  * new-library timingx
  * set-text
    """
    function edge(x) -> rising
        prev = delay(x, ticks: 1)
        rising = x & !prev
    end
    """
  * new-script Lights
  * place-block lamp as lamp
  * set-text
    """
    p = player_input()
    t = simple_telemetry()
    power_display(t.speed)
    toggler(timingx.edge(p.horn), targets: placed("{uid:lamp}"))
    """
  * expect-no-errors
  * compile
  * expect-result Placed
  * place-start
  * test-mode
  * wait 2
  * back-to-editor
  * open-window
  * select-script Lights
  * screenshot right-away
  * wait 3
  * screenshot after-wait
  * expect-no-errors

Scenario Outline: footer fits at <h> px
  * set-window-height <h>
  * expect-footer-in-view
  * screenshot scripts-<h>
  Examples:
    | h   |
    | 64  |
    | 120 |
    | 240 |
    | 640 |
```

### Language facts ([Gherkin reference](https://cucumber.io/docs/gherkin/reference/))

- Doc strings are fenced with `"""` or three backticks, and "Each line of the Doc String will be dedented according to the opening `"""`". That's exactly what `set-text` needs, with no `end` collision.
- Data tables are passed to the step as its last argument.
- "the `Scenario Outline` is run *once for each row* in the `Examples` section". A `Background` runs before each scenario, and `Rule` groups scenarios.
- `*` can stand in for Given/When/Then/And/But, and "Keywords are not taken into account when looking for a step definition."
- Comments are `#` at the start of a line only. There are no block comments.
- Repetition is per *scenario* (Outline), not a loop inside one. Named values aren't in the language; steps share state through the context, as `PlacedBlockNames` does today.

### Parser and bindings

- [Gherkin on NuGet](https://www.nuget.org/packages/Gherkin/) 42.0.1 (2026-08-05, MIT) targets **netstandard2.0**, and its only dependency is Cucumber.Messages 34.2.1 (netstandard2.0, no dependencies). The repo's spec tests also run on `net472` on Windows (`Gherkin.Specs.csproj`: `$(TargetFrameworks);net472` when on Windows; [repo](https://github.com/cucumber/gherkin)).
- Errors: `ParserException` has `Location` (line, column), so per-line errors are kept. Running in Unity Mono is **unverified**, but it's a plain netstandard2.0 library and the game ships `netstandard.dll`.
- Step matching: [Cucumber.CucumberExpressions](https://www.nuget.org/packages/Cucumber.CucumberExpressions/) 20.1.0 (MIT, netstandard2.0, no dependencies) supports `{int}`, `{float}`, `{word}`, `{string}` and custom parameter types ([docs](https://cucumber.io/docs/cucumber/cucumber-expressions/)). A mod could register `place-block {word} as {word}`, or its own pattern, and the harness matches it.
- [Reqnroll](https://www.nuget.org/packages/Reqnroll/) 3.3.4 (2026-03-23, BSD-3-Clause, the SpecFlow successor) supports .NET Framework 4.6.2/4.7.2/4.8.1 and .NET 8–10 with NUnit, MSTest, TUnit and xUnit ([compatibility](https://docs.reqnroll.net/latest/installation/compatibility.html)). It generates unit-test classes at build time and depends on System.Text.Json 8, Microsoft.Extensions.DependencyModel and others (NuGet metadata). It's built for running under a test runner, not as a coroutine inside a game. Use only the parser and the expressions packages, with our own binding attributes. Running Reqnroll's runtime under Zeepkist's Mono is **unverified** and not recommended.

### Editor support

- The [Cucumber VS Code extension](https://github.com/cucumber/vscode) (Cucumber Language Server) autocompletes steps from step definitions found in source, C#/SpecFlow among the languages it supports. Whether it recognizes our own attributes, or only SpecFlow/Reqnroll's `[Given]`/`[When]`/`[Then]`, is **unverified**. It finds definitions by reading C# source (`cucumber.glue`), not from a registry, so steps registered in code (`RegisterStep("x", …)`) would not be seen.
- Reqnroll lists Visual Studio 2022/2026 support plus Rider and VS Code integrations.
- Completion is pattern-based. It doesn't know a step's choices (`scripts|libraries`) unless they're written into the pattern.

Verdict: the `*`-step variant is a real option if a standard, line-numbered parser for the current style matters more than editor completion. Given/When/Then prose adds words without adding checks for this kind of UI-driving test, and agents tend to invent sentences that no binding matches.

## 6. Robot Framework (design precedent only)

Robot Framework 7.x ([User Guide](https://robotframework.org/robotframework/latest/RobotFrameworkUserGuide.html), 7.5.1 at time of reading) is keyword-driven plain text:
- Cells are separated by two or more spaces, under `*** Settings ***`, `*** Variables ***`, `*** Test Cases ***` and `*** Keywords ***`.
- `${scalar}`, `@{list}` and `&{dict}` variables. A keyword's return value is assigned with `${value} =    Get Some Value`.
- User keywords with `[Arguments]`, and named arguments as `name=value`.
- `FOR`/`WHILE`/`IF`/`TRY`/`GROUP` blocks closed by `END`, and `...` continuation rows.

```robotframework
*** Test Cases ***
Placed block still found after test mode
    Open Window
    New Library    timingx
    Set Text    function edge(x) -> rising\n${SPACE*4}prev = delay(x, ticks: 1)\n${SPACE*4}rising = x & !prev\nend
    New Script    Lights
    ${lamp}=    Place Block    lamp
    Set Text    p = player_input()\nt = simple_telemetry()\npower_display(t.speed)\ntoggler(timingx.edge(p.horn), targets: placed("${lamp}"))
    Expect No Errors
    Compile
    Expect Result    Placed
    Place Start
    Test Mode
    Wait    2
    Back To Editor
    Open Window
    Select Script    Lights
    Screenshot    right-away
    Wait    3
    Screenshot    after-wait
    Expect No Errors

Footer fits at each height
    FOR    ${h}    IN    64    120    240    640
        Set Window Height    ${h}
        Expect Footer In View
        Screenshot    scripts-${h}
    END
```

What it shows:
- `${x}=  Keyword` is the cleanest "named value from an earlier step" in any of these formats. `FOR … END` shows a loop needs only literal lists.
- Libdoc's JSON spec files feeding the RobotCode LSP are the precedent for `steps.json` → editor.
- Its weakness for us is multi-line text: cells are space-separated, so indentation inside script text needs `\n` and `${SPACE}` escapes (the escaping is described in the User Guide; the exact rendering above is **unverified**).
- It runs on Python, so it can't run in the game, and its syntax only has value as a design source.

## 7. KDL v2

### Syntax ([KDL 2.0.0 spec](https://github.com/kdl-org/kdl/blob/main/draft-marchan-kdl2.md), released 2024-12-22)

- A document is nodes. "Every node must have a name". After the name come arguments (ordered) and `key=value` properties, and an optional `{ … }` children block. A node ends at a newline, a `;`, a `}` or EOF.
- A bare identifier string can't start with a digit, can't contain `(){}[]/\"#;=` or whitespace, and can't be `true`/`false`/`null`/`inf`/`nan` without `#`. Booleans are `#true`/`#false`.
- Multi-line strings use `"""` and "dedent" by the closing line's whitespace prefix. They may contain unescaped `"` (just not `"""`). Raw strings (`#"…"#`, `#"""…"""#`) turn off `\` escapes.
- Comments are `//` and `/* */`, plus `/-` slashdash to comment out a node, argument or children block.

```kdl
// The showcase's Script: does placed(...) still find the lamp after test mode, once the window has rechecked?
open-window
new-library timingx
set-text #"""
    function edge(x) -> rising
        prev = delay(x, ticks: 1)
        rising = x & !prev
    end
    """#
new-script Lights
place-block lamp as=lamp
set-text #"""
    p = player_input()
    t = simple_telemetry()
    power_display(t.speed)
    toggler(timingx.edge(p.horn), targets: placed("${lamp}"))
    """#
expect-no-errors
compile
expect-result Placed
place-start
test-mode
wait 2
back-to-editor
open-window
select-script Lights
screenshot right-away
wait 3
screenshot after-wait
expect-no-errors

for "h" 64 120 240 640 {
    set-window-height "${h}"
    expect-footer-in-view
    screenshot "scripts-${h}"
}
```

That's the closest to today's look, with real nesting for loops. The friction points:
- Free text must be quoted once it has spaces: `new-script "Horn booster"`, `expect-result "Placed 3 blocks"`, `expect-error "2:15" "'nope' isn't defined"`.
- `#` is not a comment, which trips anyone used to the line DSL, YAML or shell.
- Anything built from `${h}` must be a quoted string, since `$` and `{` can't appear in a bare identifier.

### C# parsers ([KDL implementations list](https://github.com/kdl-org/kdl#implementations))

| Package | Version / date | License | Target | KDL | Notes |
| --- | --- | --- | --- | --- | --- |
| [KdlSharp](https://www.nuget.org/packages/KdlSharp/) ([repo](https://github.com/AndreyAkinshin/KdlSharp)) | 1.1.0, 2026-02-09 (last commit 2026-03-02) | MIT | **netstandard2.1 only** | v1 + v2 (`KdlVersion.V2/V1/Auto`) | `KdlParseException.Line/Column`, `SourcePosition` on every node and value, serializer, KDL Schema validation, queries |
| [Kadlet](https://www.nuget.org/packages/Kadlet/) | 0.1.0, 2022-07-23 | MIT | netstandard2.0 | **v1 only** | port of kdl4j; errors "At line N, in position M" |
| [KdlDotNet](https://www.nuget.org/packages/KdlDotNet/) | 1.1.0, 2021-10-04 | CC-BY-4.0 | netstandard2.0, tested on net472 | **v1 only** | "up to date as of the KDL 1.0.0 spec" |
| Kuddle.Net 0.8.0, KDLib 0.6.0 | 2026 | — | net10.0 / net8.0 | — | can't run in the game |
| Kdl.NetStandard | — | — | — | — | unrelated: a robotics kinematics library ("Orocos Kinematics and Dynamics Library") |

The KDL README says KdlSharp works on ".NET FW 4.7.2+". That conflicts with its single `netstandard2.1` target: .NET Framework doesn't implement netstandard2.1 ([.NET Standard versions](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)), so a net472 project can't reference the package through NuGet.

There are two ways around it:
1. **The game's runtime is netstandard2.1-capable.** `Zeepkist.GameLibs/package/lib/netstandard.dll` is version **2.1.0.0** and forwards `IAsyncEnumerable<T>`, `System.HashCode`, `System.Index` and `System.Range`, and the game's `mscorlib` defines them. That matches Unity 2021.3, whose .NET Framework profile is ".NET Framework 4.8 … plus additional APIs in .NET Standard 2.1" ([Unity .NET profile support](https://docs.unity3d.com/2021.3/Documentation/Manual/dotnetProfileSupport.html)). A netstandard2.1 DLL referenced directly, not through a PackageReference, will probably load in the game. **Unverified.**
2. **Fork to netstandard2.0.** KdlSharp is about 12k lines. A scan of its source finds few netstandard2.1-only uses: 3 `IAsyncEnumerable` streaming APIs (which can be dropped), 1 `HashCode`, 1 `[^…]` index, 1 `Split(char)`, and 2 `NotNullWhen` (which can be polyfilled). That's a small fork. I didn't compile it, so **unverified**.

### Editor support

The KDL README lists IntelliJ, VS Code (KDL 2.0), Sublime, Tree-sitter, vim, Kate, Zed and a `kdl-lsp` (Rust, in kdl-rs). These are syntax-level tools. None uses a KDL Schema ([SCHEMA-SPEC](https://github.com/kdl-org/kdl/blob/main/SCHEMA-SPEC.md)) for completion as far as found, so per-mod step completion would need our own LSP, the same cost as §1.

Verdict: a nicer *syntax* than YAML for this, but it brings the line DSL's tooling cost back and adds a parser we'd fork or load outside NuGet. Not worth it over the improved line DSL or YAML.

## 8. Others, briefly

- **TOML** ([Tomlyn](https://www.nuget.org/packages/Tomlyn/) 2.10.1, BSD-2-Clause; on netstandard2.0 it needs System.Text.Json 10, System.Collections.Immutable 9 and System.Buffers). An ordered list of different steps becomes `[[steps]]` tables or inline tables, which is worse than YAML. Multi-line `"""` strings are fine. Not a fit.
- **Hurl** ([file format](https://hurl.dev/docs/hurl-file.html), [captures](https://hurl.dev/docs/capturing-response.html)): a request/response format with `[Captures]` that become `{{variables}}` and multi-line string bodies ([request](https://hurl.dev/docs/request.html)). It shows a custom text format can carry captures and multi-line bodies cleanly. The parser is Rust and specific to HTTP.
- **AltTester** ([overview](https://alttester.com/docs/sdk/latest/pages/overview.html)): the AltTester Unity SDK "is a Unity plugin used to instrument your Unity game". The instrumented app connects over WebSocket to AltTester Server, and tests in C#, Python, Java or Robot Framework drive it through AltDriver. That means building the game with the SDK, which mod authors can't do (a BepInEx port is **unverified**). Its idea of external tests in a real language through an object-finding driver is the C#-test tier, not a file format.
- **GameDriver**: also an in-engine agent plus external tests, with HierarchyPath (HPath) queries to find objects ([features](https://gamedriver.io/features), [HPath plugin tests](https://kb.gamedriver.io/hierarchypath-plugin-driven-testing)). Commercial and built into the project. HPath-style selectors could inspire a generic `click: { path: … }` core step.
- **Unity Test Framework**: `[UnityTest]` "runs as a coroutine" and "If you `yield return null`, you skip a frame" ([UTF manual](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-attribute-unitytest.html)). This is the model for the C# `[InGameTest] IEnumerator` tier in generic-tester.md §7, not a scenario format.
- **Playwright codegen** ([docs](https://playwright.dev/docs/codegen)) "will look at your page and figure out the best locator", records actions into Node.js/Python/Java/.NET tests, and can add "assert visibility/text/value". The equivalent for us would be a "record" mode in the harness that writes Scenario steps while someone plays. That's easier to emit as YAML or line DSL than as Lua or C#, and worth noting as a later feature for either.

## Recommendation

1. **Build the typed step registry first, in `Zeepkist.Tester.Sdk`, whichever format wins.** Each step declares its name, namespace, docs, typed arguments (word, text, block text, int, seconds, pixels, choice, keys, file-name, produced name) and what it produces. It replaces `StepList` + `StepArgument`. The CLI dumps it to `steps.json` and generates the step reference Markdown, which retires the hand-kept table and `StepListTests`.
2. **Use YAML (`*.ztest.yaml`) in Maestro's shape as the scenario format.**
   - Parse with YamlDotNet's representation model (the `net47` build, internalized or ILRepacked to avoid clashes), keep scalars as strings, convert them through the registry's argument types, and report errors as `file:line:column: <step> takes …`.
   - The CLI generates `steps.schema.json` per mod (draft-07 or 2020-12, with `markdownDescription` for hover) and writes a `yaml.schemas` entry into `.vscode/settings.json`. Scenarios carry `# yaml-language-server: $schema=…`, which Rider reads too through `# $schema:` (support both, or document the IntelliJ form).
   - Control nodes are data only: `for: {name: [literals]} + steps`, `repeat: {times: N, steps}`, `run: file` with `with:` parameters. `as:` names a produced value, and `${name}` substitutes it in any text argument.
   - No expressions. Logic beyond that goes in C# `[InGameTest]` (and Lua, if generic-tester.md §3 is kept).
3. **`ztest check <files>`** validates offline against the same registry, with no game, so agents get a fast loop. Give agents the schema path and a three-line quoting rule in the repo's AGENTS.md.
4. **Fallback if YAML gets in the way:** the improved line DSL with `steps.json` and a C# language server (`ztest lsp`) behind a thin VS Code extension and an LSP4IJ entry for Rider. The registry from step 1 is the expensive, shared part. The parser front-end is small either way, so switching later stays cheap.

Why YAML over the improved line DSL: the two are close on authoring. YAML wins on what the harness doesn't have to build and maintain: completion, validation and hover docs in all three editors the authors use, from one generated file. It also removes the hand-written argument parsing per step and the `end` collision.

## Migration path from `.zlstest`

1. **Registry.** Move today's ~70 steps to the typed spec, keeping their names and messages. The old `ScenarioParser` reads through the registry (`Parse` → name + argument string → spec), so `.zlstest` files keep working unchanged.
2. **Converter.** `ztest convert foo.zlstest` writes `foo.ztest.yaml` mechanically:
   - each line becomes `- step: arg`, with the argument split by the spec (`place-block lamp as lamp` → `{ function: lamp, as: lamp }`, `set-window-size 800 600` → `{ width: 800, height: 600 }`);
   - `set-text … end` becomes `set-text: |` with the body re-indented;
   - `{uid:x}` becomes `${x}`;
   - `#` comments are kept as YAML comments;
   - scalars are quoted where YAML needs it (`:`, `#`, a leading `{[*&!|>'"%@`, `on`/`off`/`yes`/`no` for safety).

   Round-trip test: converting, then parsing the YAML, gives the same step list as parsing the `.zlstest`. Run it over all of `Scenarios/` and `.scratch/ingame/`.
3. **Both parsers for one release.** The harness takes `.zlstest` and `.ztest.yaml`, and the CLI warns on `.zlstest`. Convert ZeepLogicScript's Scenarios in one commit, and update `docs/ingame-tests.md` to point at the generated step reference.
4. **Add control nodes** (`for`, `repeat`, `run`) only in YAML, then collapse the copy-paste in Scenarios like `214-window-min-height`.
5. **Remove the line parser** once no repo has `.zlstest` files. Keep `ztest convert` for old agent scratch files.

## Unverified items

- YamlDotNet, Gherkin, and a netstandard2.1 KdlSharp loading and running inside Zeepkist's Mono under BepInEx.
- Whether any other Zeepkist mod ships its own YamlDotNet (an assembly version clash).
- Visual Studio 2022's schema-driven YAML IntelliSense (one Microsoft Q&A answer only).
- Rider importing a TextMate grammar for a custom extension. LSP4IJ working in Rider with a custom server (LSP4IJ says "all flavours of IntelliJ", which wasn't tested with Rider here).
- Whether the Cucumber VS Code extension finds step definitions that use non-SpecFlow attributes.
- Newtonsoft trailing-comma tolerance.
- The exact Robot Framework escaping for multi-line text.
- That the KdlSharp fork to netstandard2.0 compiles. The build was not run; the estimate comes from scanning the source.
