# Step registry: API sketch

The typed list of steps that the YAML parser, the editor schema, the step reference docs, `ztest check`, `ztest convert` and C# tests all come from. It replaces ZeepLogicScript's `StepList` + `StepArgument` + the hand-kept step table + `StepListTests`. Background: [generic-tester.md](../research/generic-tester.md) §4 and §7, and [scenario-format.md](../research/scenario-format.md).

Status: sketch. The attributes, argument types and descriptors are built (#3), and so are reading them offline (#4) and named values (#5); the rest isn't, and its names are placeholders.

## The one constraint that shapes it

**The registry must be readable without running the game.** `ztest check`, `ztest schema` and `ztest convert` run on the modder's machine, with no game, in a .NET 8+ CLI. They read the built test plugin DLLs with `System.Reflection.MetadataLoadContext`, which reads metadata only and runs no code, plus the XML doc file next to each DLL.

So a step is described by **attributes and property types only**. There's no registration code that has to run, and no custom parser objects the CLI would have to execute. What isn't static stays inside `Run`, which only the game executes. The CLI can then:
- list every step of the core and each mod, with docs taken from `<summary>`;
- produce the JSON Schema;
- check a scenario's types, enums, required arguments and names (`as:` defined before `${…}` use), all with line and column.

## A step

```csharp
/// <summary>Waits, such as for the level to run a little before a screenshot.</summary>
[Step("wait")]
public sealed class Wait : Step
{
    /// <summary>How long to wait.</summary>
    [Arg(Primary = true)] public Seconds Seconds { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        yield return context.WaitSeconds(Seconds);
    }
}
```

- `Step` is an abstract base with `IEnumerator Run(IStepContext)`, a coroutine as today.
- Each `[Arg]` property is one YAML key. Its name is the property name in kebab case (`Seconds` → `seconds`), unless the attribute gives one.
- `Primary = true` lets the step take that argument as a plain scalar: `- wait: 2` is the same as `- wait: { seconds: 2 }`. A step with no arguments is a bare string (`- open-window`).
- A required argument is a C# `required` property (`[Arg] public required string Function { get; set; }`). The compiler then enforces it in C# tests (`new PlaceBlock { }` doesn't compile), and the CLI reads it from the property's `RequiredMemberAttribute`. That attribute and `CompilerFeatureRequiredAttribute` need a polyfill on net472, which the SDK ships. Everything else is optional: nullable, or with `[Arg(Default = …)]`.
- The `<summary>` on the class and on each property is the step's documentation: the reference docs, the schema's `markdownDescription` and hover text.

### Argument types

A fixed set, so the CLI can describe and check each one without running anything:

| C# type | YAML | Schema | Notes |
| --- | --- | --- | --- |
| `string` | scalar | `string` | One line. |
| `TextBlock` | `\|` block or scalar | `string` | Several lines, such as a Script's text. |
| `int`, `float`, `bool` | scalar | `integer` / `number` / `boolean` | `bool` takes only `true`/`false`, never `on`/`yes`. |
| `Seconds` | `2`, `0.5`, `500ms` | `number` or `^\d+(\.\d+)?(ms\|s)$` | |
| `Pixels` | `-1000` | `integer` | Negative is allowed. |
| an `enum` | scalar | `enum` | `[ArgValue("global_velocity")]` on a member names how it's written. Choices like `scripts\|libraries` and `on\|off` become enums, which is also why `on` is safe. |
| `Keys` | `ctrl+space escape` | `string` with a pattern | The existing key-chord syntax (`KeyNames`), checked by pattern. |
| `FileName` | scalar | `^[A-Za-z0-9_-]+$` | For screenshot names. |
| `Name<T>` | scalar | `^[A-Za-z0-9_-]+$` | **Defines** a named value of kind `T` (see *Named values*). |
| `Ref<T>` | scalar | `^[A-Za-z0-9_-]+$` | **Uses** one, by name. |
| `List<T>` of any of these | sequence | `array` | |
| a class with `[Arg]` properties | map | `object` | For nested arguments, such as a drag's moves. |

Patterns and ranges come from attributes the CLI can read: `[Pattern("…")]`, `[InRange(1, 8)]`, `[MinItems(1)]`.

## Named values

Today `place-block lamp as lamp` remembers a block for `{uid:lamp}` and `set-block-setting lamp …`. That becomes a typed named value:

```csharp
/// <summary>A block placed or named by a step, by its UID.</summary>
[ValueKind("block", Substitute = nameof(Uid))]   // ${lamp} writes the UID
public sealed class BlockValue
{
    public string Uid { get; init; }
}

/// <summary>Places a block of the Block Function's type in its first style, beside where place-start puts the start block, and selects it.</summary>
[Step("place-block")]
public sealed class PlaceBlock : Step
{
    /// <summary>The Block Function, such as <c>lamp</c>.</summary>
    [Arg(Primary = true)] public string Function { get; set; }

    /// <summary>A name for the block, so later steps can write <c>${name}</c> for its UID.</summary>
    [Arg] public Name<BlockValue>? As { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var block = /* … places it, as today … */;
        As?.Set(context, new BlockValue { Uid = block.UID });
    }
}

/// <summary>Changes a Setting of a named block, as changing it in the block's settings panel does, as one undo step.</summary>
[Step("set-block-setting")]
public sealed class SetBlockSetting : Step
{
    [Arg] public Ref<BlockValue> Block { get; set; }
    [Arg] public string Setting { get; set; }
    [Arg] public string Value { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var block = Block.Get(context);   // fails the step, naming the name, if it isn't defined
        // …
    }
}
```

```yaml
- place-block: { function: lamp, as: lamp }
- set-block-setting: { block: lamp, setting: invert, value: "true" }
- set-text: |
    toggler(p.horn, targets: placed("${lamp}"))
```

- Names last for the scenario, except inside a `for`, `repeat` or `run:` (see *Control nodes and names*). Defining a name that's already defined is an error, as today.
- `${name}` works in any single YAML value, whatever the argument's type. It's substituted before the value is converted (see *Control nodes and names*). It needs a name whose kind has a `Substitute` member, a public string property. A `${name}` that isn't defined by an earlier step fails `ztest check` with its line. `$${` writes a literal `${`, and any other `${` without a name and `}` after it is an error.
- In the game, a mistake with a name throws `StepFailedException`, which stops the step and fails it with the exception's message, so `Run` needs no check after `Block.Get(context)`. The scenario runner (#13) records that as the step's failure. It also makes one `ScenarioValues` per scenario, with a scope for each body. Substitution moves into binding (#7, #13), so `StepArguments.Substitute` from #5 goes away.
- Descriptors record this for `ztest check`: `ArgumentKind.Name` defines a name and `ArgumentKind.Ref` uses one, each with its `ValueKind` and its `Substitute` member.
- `ztest check` follows the steps in order, so it knows which names exist at each line, with a scope for each `for`, `repeat` and `run:` body.

## The context

```csharp
public interface IStepContext
{
    string Scenario { get; }
    ManualLogSource Logger { get; }

    // Outcome, as StepContext today
    string Detail { get; set; }
    void Pass(string detail, string expected, string actual);
    void Fail(string detail, string expected = null, string actual = null);
    bool Failed { get; }

    // Waiting; each wait that gives up fails the step, saying what it waited for
    IEnumerator WaitSeconds(Seconds seconds);
    IEnumerator WaitFrames(int frames);
    IEnumerator WaitUntil(Func<bool> condition, Seconds timeout, string what);

    // What the harness provides
    IInput Input { get; }          // see below
    IGameEvents Game { get; }      // counts of scene loads, editor entries, test mode entries, round starts, level loads
    IExceptionLog Exceptions { get; }
    IEnumerator Screenshot(FileName name);
    LEV_LevelEditorCentral RequireEditor();   // fails the step and returns null outside the editor

    // Named values, and state a mod keeps for the length of one scenario
    IValues Values { get; }
    T Scenario<T>() where T : class, new();   // made on first use, dropped when the scenario ends

    // Things a mod's plugin gave the harness, such as its window driver
    T Get<T>() where T : class;
}
```

`Scenario<T>()` replaces fields like `PlacedBlockNames` and `RememberedWindowRect` that ZLS's `StepContext` has today. Each mod keeps its own state class instead of growing the shared context.

### Input

```csharp
public interface IInput
{
    InputRoute Route { get; set; }   // per scenario; `input: os` in the YAML header, or a step that switches it

    IEnumerator Press(Keys keys);    // in one frame, as `press` today
    IEnumerator Type(string text);   // one key a frame
    IEnumerator Hold(Keys keys, Seconds duration);
    IEnumerator MoveMouse(Vector2 screenPoint);
    IEnumerator Click(Vector2 screenPoint, MouseButton button = MouseButton.Left);
    IEnumerator Drag(Vector2 from, IReadOnlyList<DragMove> moves, MouseButton button = MouseButton.Left);
    void GiveBack();                 // the player's mouse again, as ImuiMouse.GiveBack; the harness calls it after each scenario
}

public enum InputRoute
{
    InProcess,   // Imui events + Harmony on Input + Rewired action fields + uGUI pointer events: same frame, no focus needed
    Os,          // SendInput: what a player's keyboard and mouse do; needs the window in front
}
```

Core steps (`press`, `type`, `hold`, `click`, `move-mouse`, `drag`) are thin wrappers over this. A mod's step uses it the same way: ZLS's `hover` and `drag-window-edge` would find where to point from its window driver, then call `context.Input`.

## Nested arguments: `drag-window-edge`

Today it's `drag-window-edge -1000 over 10 then 200 over 2`, read by a hand-written parser. In the registry:

```csharp
/// <summary>One move of a drag, in even steps over the frames.</summary>
public sealed class DragMove
{
    /// <summary>Pixels down, or up for a number below 0.</summary>
    [Arg(Primary = true)] public Pixels By { get; set; }

    /// <summary>Frames the move takes.</summary>
    [Arg(Default = 1), InRange(1, 600)] public int Over { get; set; }
}

/// <summary>Drags the window's resize handle with the mouse, the way a player does …</summary>
[Step("drag-window-edge", Namespace = "zls")]
public sealed class DragWindowEdge : Step
{
    /// <summary>The moves, made in turn before letting go.</summary>
    [Arg(Primary = true), MinItems(1)] public List<DragMove> Moves { get; set; }

    [Arg(Default = false)] public bool Sideways { get; set; }   // replaces drag-window-edge-sideways

    public override IEnumerator Run(IStepContext context) { /* … */ }
}
```

```yaml
- zls.drag-window-edge: 300                                     # one move, over 1 frame
- zls.drag-window-edge: [{ by: -1000, over: 10 }, { by: 200, over: 2 }]
- zls.drag-window-edge: { moves: [-50], sideways: true }
```

A `Primary` list also takes a single item, and a nested class with its own `Primary` takes a scalar. So the short forms fall out of the same two rules, and the schema describes them as `oneOf`.

## A mod's steps

```csharp
[BepInPlugin(…)]
[BepInDependency(TesterInfo.Guid)]
[BepInDependency(LogicScripting.MyPluginInfo.PLUGIN_GUID)]
[TestPlugin(Namespace = "zls")]          // the CLI finds the steps from this, with no code run
public class Plugin : BaseUnityPlugin
{
    private void Awake()
    {
        // The game side registers the same steps, plus what they run against
        var window = /* the Logic Scripting plugin's ScriptWindow */;
        Tester.Register(this)
            .Service(new ScriptWindow.Driver(window))
            .Hooks(new LogicScriptingHooks());
    }
}

/// <summary>Compiles the selected Script, as Ctrl+Enter does. It passes whatever the Compile did.</summary>
[Step("compile", Namespace = "zls")]
[Requires(typeof(ScriptSelected))]       // see Preconditions: checked before Run
public sealed class Compile : Step
{
    public override IEnumerator Run(IStepContext context)
    {
        context.Get<ScriptWindow.Driver>().Compile();
        yield return null;
    }
}

/// <summary>What a run puts back around each of this mod's scenarios.</summary>
public sealed class LogicScriptingHooks : IScenarioHooks
{
    private ScriptWindow.KeptState _start;

    public void RunStarted(IRunContext run) => _start = run.Get<ScriptWindow.Driver>().RememberKeptState();

    public IEnumerator BeforeScenario(IScenarioContext scenario)   // not before the first
    {
        var window = scenario.Get<ScriptWindow.Driver>();
        window.DeleteLibraries();
        window.PutBackKeptState(_start);
        yield break;
    }

    public IEnumerator AfterScenario(IScenarioContext scenario) { yield break; }   // also after a failed one
}
```

Step classes are found by scanning the plugin's assembly for `[Step]`, both by `Tester.Register` in the game and by the CLI offline. `Namespace` defaults to the `[TestPlugin]` one.

Namespacing (decided):
- **The steps the tester ships with have no prefix:** `wait`, `screenshot`, `press`, `click`, `place-block`, `test-mode`, …. The empty namespace is reserved for them, so no mod can register one there.
- **A mod's steps are always prefixed** (`zls.compile`), unless the scenario's header lists the namespace under `use:`. Then it may write them unprefixed.
- **A clash is a load error that names both steps,** and tells you to write the step prefixed. That covers a `use:`d step with the same name as a core step, and two `use:`d namespaces with the same step. Without `use:`, a new core step or a new step in another mod can never break a scenario. With it, the error points at the one line to change.

```yaml
use: [zls]
steps:
  - open-window
  - compile
```

## Preconditions

Many steps only make sense in a certain state. Today each step checks this itself in `Run` (`RequireOpenWindow`, `RequireScript`, `RequireFocusedField`, `RequireEditor`) and fails saying which step to add first. In the registry, states are declared, so one description gives both the runtime check and an offline one.

### States

A state is a class: a name, how to check it in the game, and a hint for the failure message.

```csharp
/// <summary>The level editor, not in test mode.</summary>
[State("editor", Hint = "Start with `start: editor`, or add `back-to-editor`.")]
public sealed class InEditor : IState
{
    public bool IsMet(IStepContext context) => LevelEditorApi.IsInLevelEditor && !LevelEditorApi.IsTestingLevel;
}

[State("test-mode", Hint = "Add `test-mode` first.")]
public sealed class InTestMode : IState { /* … */ }

/// <summary>The Script window is open.</summary>
[State("window-open", Namespace = "zls", Within = typeof(InEditor), Hint = "Add `zls.open-window` first.")]
public sealed class WindowOpen : IState { /* … */ }

[State("script-selected", Namespace = "zls", Within = typeof(WindowOpen), Hint = "Add `zls.new-script` or `zls.select-script` first.")]
public sealed class ScriptSelected : IState { /* … */ }

[State("field-focused", Namespace = "zls", Within = typeof(WindowOpen), Hint = "Add `zls.caret-at-end` first.")]
public sealed class FieldFocused : IState { /* … */ }
```

`Within` makes states nest. Leaving a state leaves every state within it: `close-window` ends `zls.script-selected` too, and `test-mode` ends everything within `editor`.

### Steps declare what they need and what they change

```csharp
[Step("compile", Namespace = "zls")]
[Requires(typeof(ScriptSelected))]
public sealed class Compile : Step { /* … */ }

[Step("open-window", Namespace = "zls")]
[Requires(typeof(InEditor)), Enters(typeof(WindowOpen))]
public sealed class OpenWindow : Step { /* … */ }

[Step("close-window", Namespace = "zls")]
[Leaves(typeof(WindowOpen))]
public sealed class CloseWindow : Step { /* … */ }

[Step("test-mode")]
[Requires(typeof(InEditor)), Leaves(typeof(InEditor)), Enters(typeof(InTestMode))]
public sealed class TestMode : Step { /* … */ }
```

`[Requires]` takes several states, all of which must hold. For "either", an `[RequiresAny(typeof(A), typeof(B))]` form covers it.

### Checked in two places

- **In the game, before `Run`:** the harness calls `IsMet` for each required state, and on a miss fails the step without running it: `zls.compile needs zls.script-selected: Add zls.new-script or zls.select-script first.` That replaces the hand-written `Require…` calls and gives every step the same message. `Run` can still check things a state can't express, such as whether a named Script exists.
- **Offline, in `ztest check`:**
  - It follows the scenario from its `start:` state, through each step's `Enters` and `Leaves`. A step whose required state doesn't hold at that point is reported with its line and the hint.
  - `run:` sub-scenarios are followed into.
  - A `for` or `repeat` body is checked once, then again starting from the state it ends in, so a body that closes the window it needs is caught.
  - These are reported as **warnings**, not errors, because the offline model is only as good as the declarations. A step whose effect depends on the game (`back-to-editor` after a failed `test-mode`) can leave the model out of step with the game, and the runtime check is the one that counts.
  - States a mod declares but no step `Enters` are listed as a warning by `ztest check --registry`, so a missing declaration shows up early.

The states are also in the generated docs: each step's entry says what it needs and what it changes, and each state lists the steps that enter it. That answers "what do I add before this?" without reading the code.

## Scenario file

The parser lives in `Zeepkist.Tester.Scenarios`, a netstandard2.0 project that both the plugin (net472) and `ztest check` (net10.0) reference. NuGet still picks YamlDotNet's build per consumer: the plugin gets `net47`, and the CLI and the unit tests get `net10.0`. So the unit tests don't run the build the game does, and a run's `yaml parser` step does. The build merges the plugin, the parser and YamlDotNet's `net47` build with ILRepack into `Zeepkist.Tester/bin/<configuration>/net472/plugin/`, with YamlDotNet internal so another mod's copy can't clash. That folder, with the SDK next to the merged DLL, is what a run deploys. The tests and the CLI keep using the separate DLLs.

```yaml
# yaml-language-server: $schema=../.ztest/schema.json
description: A Script drives a block in test mode, and is still there back in the editor
use: [zls]
start: editor            # main-menu | editor | editor: { level: file.zeeplevel } | free-play: { uid: … }
input: in-process        # or os
steps:
  - open-window
  - new-script: Lights
  - set-text: |
      power_display(1 & 1)
  - compile
  - expect-result: Placed 3 blocks
  - expect-blocks: 3
  - place-start
  - test-mode
  - wait: 2
  - screenshot: running
  - back-to-editor
```

The runner handles the control nodes itself; they aren't registry steps, but the schema includes them:

| Node | Form |
| --- | --- |
| `for` | `- for: { h: [64, 120, 240] }` + `steps:`. Literal lists only. `${h}` inside. |
| `repeat` | `- repeat: 3` + `steps:` |
| `run` | `- run: shared/open-lights.ztest.yaml` with optional `with: { name: value }`. Its steps report under the caller, with the file and line of both. |

### Control nodes and names

**`${x}` works in any single value.** The harness keeps each argument's YAML text, substitutes it, and only then converts it to the argument's type, so `int`, `Seconds`, `FileName`, an enum, `Name` and `Ref` all take it:

```yaml
- for: { h: [64, 120, 240] }
  steps:
    - set-window-height: ${h}
    - screenshot: scripts-${h}
```

- It only stands in for a single value, never for a whole list or map, so it doesn't grow into an expression language.
- The generated schema also accepts a `${…}` wherever it expects a non-text type, so the editor is less strict about those values.
- `ztest check` tries each `for` value against the argument's type, because the values are literals. It can't know a step's value from `as:` or a value from `with:`, so a bad one fails when the step runs, with a message giving the argument and the value.

**Each pass of a body has its own scope.** A name defined in a `for` or `repeat` body lasts until that pass ends, and the next pass starts fresh:

```yaml
- repeat: 3
  steps:
    - place-block: { function: lamp, as: lamp }
    - set-block-setting: { block: lamp, setting: invert, value: "true" }
```

- A body can read names from outside it. Defining one of those names again in the body is the same "already defined" error, so nothing gets silently hidden.
- A name defined in a body can't be used after it. The blocks stay in the level, but their names are gone. Needing them afterwards takes a C# test, or writing those steps out.
- A `run:` file has its own scope too, and sees only what its `with:` passes in, so a shared file doesn't depend on who calls it.

**A loop variable is an ordinary named value.** The SDK has a built-in `text` kind, whose `Substitute` member is the text itself. Each pass defines the `for` variable as one in that pass's scope, and `with:` parameters are the same. All the rules for names then apply to them.

- A value can name another value, because substitution comes first. `block: ${b}` with `for: { b: [lamp, gate] }` becomes `block: lamp`, which looks up the block called `lamp`.
- A `for` has **one variable**, and more than one key is a load error. `for: { w: [...], h: [...] }` could mean pairs or every combination. Nesting two `for`s gives every combination, and pairs can wait until a scenario needs them.

**Each pass binds fresh steps.** The runner creates new step objects from the YAML text for every pass, so nothing is substituted twice and a `$${` is unescaped once.

## C# tests

The same steps, from C#, for when a scenario needs real logic:

```csharp
public sealed class WindowTests
{
    [InGameTest(Start = Start.Editor)]
    public IEnumerator MinimumHeightKeepsTheFooter(IStepContext context)
    {
        yield return context.Run(new OpenWindow());
        foreach (var height in new[] { 64, 120, 240, 640 })
        {
            yield return context.Run(new SetWindowHeight { Pixels = height });
            yield return context.Run(new ExpectFooterInView());
            if (context.Failed)
                yield break;
        }
    }
}
```

- `context.Run(step)` runs a step as one entry in the results, with its own pass or fail.
- The test method stops at the first failed step, as a scenario does.
- An `[InGameTest]` shows in the results like a scenario does, named `WindowTests.MinimumHeightKeepsTheFooter`.
- `${x}` belongs to the YAML format. `context.Run` doesn't change a step's arguments, so one step object can run twice, and `$$` stays what it looks like. A test that needs a named value's text asks for it: `context.Values.Substitute("placed(\"${lamp}\")")`, or `context.Values.Get<BlockValue>("lamp").Uid`. A `${lamp}` pasted from a scenario stays literal text in C#, so the step gets `${lamp}` as written.

## What's generated from it

| Output | By | From |
| --- | --- | --- |
| `.ztest/schema.json` | `ztest schema` (also every `ztest` run) | Core + the repo's `[TestPlugin]` assemblies. Each step is a `oneOf` branch: a bare string, `{name: scalar}` for `Primary`, or `{name: {…}}` with `additionalProperties: false`. |
| `.vscode/settings.json` `yaml.schemas` entry | `ztest init` | `**/*.ztest.yaml` → the schema |
| Step reference (Markdown) | `ztest docs` | `<summary>` docs, arguments, types, defaults, an example per step from `[Example("…")]` |
| Offline checks | `ztest check <files>` | The registry; reports `file:line:col` |
| `.zlstest` → YAML | `ztest convert` | The registry plus a `[LineSyntax("<function> [as <as>]")]` attribute per old step; see below |

## Moving ZLS's steps over

- Each of the ~70 steps becomes a class with `[Step]` and typed `[Arg]`s, keeping its name and messages.
- Its old line form is kept as data, `[LineSyntax("<function> [as <as>]")]`, and one generic reader turns a `.zlstest` line into the same typed arguments. That reader is used by `ztest convert`, and by the harness for one release while both formats are accepted.
- Old forms the syntax pattern can't describe get a hand-written converter in the CLI. That's `drag-window-edge`'s `then`-chains, `expect-error <line>:<column> <text>` → `{ line, column, message }`, and `set-text … end`. They're there for one release and then go.
- `StepListTests` goes too. Its job (the docs table matches the parser) is done by generating both from one place.

## Decided (2026-11-04)

- **Namespacing:** the tester's own steps are unprefixed, a mod's steps are prefixed unless `use:` lists them, and a clash is a load error (see *A mod's steps*).
- **Arguments are properties.** Required ones use C# `required`, so C# tests get compile-time checks after all.
- **Preconditions are declared:** states, plus `[Requires]`/`[Enters]`/`[Leaves]` on steps, checked in the game before each step and offline as warnings (see *Preconditions*).
- **Named values in control nodes (#40):** `${x}` works in any single value, substituted before conversion. Each pass of a `for` or `repeat` body and each `run:` file has its own scope. A loop variable is a named value of a built-in `text` kind, with one variable per `for`. Each pass binds fresh steps, and `${x}` doesn't apply to C# tests (see *Control nodes and names*).
