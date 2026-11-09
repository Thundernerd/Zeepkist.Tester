# Step registry: API sketch

The typed list of steps that the YAML parser, the editor schema, the step reference docs, `ztest check`, `ztest convert` and C# tests all come from. It replaces ZeepLogicScript's `StepList` + `StepArgument` + the hand-kept step table + `StepListTests`. Background: [generic-tester.md](../research/generic-tester.md) §4 and §7, and [scenario-format.md](../research/scenario-format.md).

Status: sketch. The attributes, argument types and descriptors are built (#3), and so are reading them offline (#4), named values (#5), the states with their runtime check (#6), the parser (#7), `ztest check` (#8), the walks to where a scenario starts (#12), the scenario runner with its context and hooks (#13) and the core steps (#14); the rest isn't, and its names are placeholders.

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
| `Keys` | `ctrl+space escape` | `string` with a pattern | The existing key-chord syntax (`KeyNames`), up to 8 keys, which is what fits in a frame, checked by pattern. |
| `FileName` | scalar | `^[A-Za-z0-9_-]+$` | For screenshot names. |
| `Name<T>` | scalar | `^[A-Za-z0-9_-]+$` | **Defines** a named value of kind `T` (see *Named values*). |
| `Ref<T>` | scalar | `^[A-Za-z0-9_-]+$` | **Uses** one, by name. |
| `List<T>` of any of these | sequence | `array` | |
| a class with `[Arg]` properties | map | `object` | For nested arguments, such as a drag's moves. |

Patterns and ranges come from attributes the CLI can read: `[Pattern("…")]`, `[InRange(1, 8)]`, `[MinItems(1)]`.

## Named values

`place-block: { type: 2448, as: lamp }` remembers a block for `${lamp}` and `set-block-setting`, as a typed named value:

```csharp
/// <summary>A block placed or named by a step, by its UID.</summary>
[ValueKind("block", Substitute = nameof(Uid))]   // ${lamp} writes the UID
public sealed class BlockValue
{
    public string Uid { get; }
}

/// <summary>Places a block on the ground below the camera, a cell to the side of where place-start puts the start block, and selects it.</summary>
[Step("place-block")]
public sealed class PlaceBlock : Step
{
    /// <summary>The block's type: its ID in the game's block list, the number a level file stores for it.</summary>
    [Arg(Primary = true)] public required int Type { get; set; }

    /// <summary>A name for the block, so later steps can write <c>${name}</c> for its UID.</summary>
    [Arg] public Name<BlockValue>? As { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var central = context.RequireEditor();
        if (central == null)
            yield break;

        var block = /* … places it … */;
        As?.Set(context, new BlockValue(block.UID));
    }
}

/// <summary>Changes a Setting of a named block, as changing it in the block's settings panel does, as one undo step.</summary>
[Step("set-block-setting")]
public sealed class SetBlockSetting : Step
{
    [Arg] public required Ref<BlockValue> Block { get; set; }
    [Arg] public required string Setting { get; set; }   // the key a level file stores it under
    [Arg] public required string Value { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        var block = Block.Get(context);   // fails the step, naming the name, if it isn't defined
        // …
    }
}
```

```yaml
- place-block: { type: 2448, as: lamp }
- set-block-setting: { block: lamp, setting: is, value: "true" }
- set-text: |
    toggler(p.horn, targets: placed("${lamp}"))
```

- `BlockValue` is in the SDK, so a mod's step can take a `Ref<BlockValue>` that `place-block` defined.
- The tester knows blocks only as the game does, so a block's type is its ID, and a setting is the key the block stores it under. A setting takes a value of the type the block already holds there: a whole number (`true` and `false` are 1 and 0), a decimal, or text. A key the block doesn't hold fails the step, listing the ones it does. The step writes what the level file stores and has the block's editor read it, with no list of which keys mean what, so a key the editor doesn't read, such as a paint slot or a wire, is only stored. A mod with its own names for types and settings has its own steps for them.
- Names last for the scenario, except inside a `for`, `repeat` or `run:` (see *Control nodes and names*). Defining a name that's already defined is an error, as today.
- `${name}` works in any single YAML value, whatever the argument's type. It's substituted before the value is converted (see *Control nodes and names*). It needs a name whose kind has a `Substitute` member, a public string property. A `${name}` that isn't defined by an earlier step fails `ztest check` with its line. `$${` writes a literal `${`, and any other `${` without a name and `}` after it is an error.
- In the game, a mistake with a name throws `StepFailedException`, which stops the step and fails it with the exception's message, so `Run` needs no check after `Block.Get(context)`. The scenario runner records that as the step's failure. It also makes one `ScenarioValues` per scenario, with a scope for each body. Substitution is part of binding.
- Descriptors record this for `ztest check`: `ArgumentKind.Name` defines a name and `ArgumentKind.Ref` uses one, each with its `ValueKind` and its `Substitute` member.
- `ztest check` follows the steps in order, so it knows which names exist at each line, with a scope for each `for`, `repeat` and `run:` body. A `Name` whose value only a run can know could define anything, so names used after it in that scope aren't called undefined.

## The context

```csharp
public interface IStepContext : IScenarioContext
{
    string ScenarioName { get; }   // from IScenarioContext; a property can't share its name with Scenario<T>()
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
    string LastScreenshotBlankColor { get; }   // the color when every pixel of the last one was the same, or null
    IEnumerator Screenshot(FileName name, bool covered = false);   // covered fills the frame with one color first, for expect-blank-screenshot
    LEV_LevelEditorCentral RequireEditor();   // fails the step and returns null outside the editor

    // Named values, and state a mod keeps for the length of one scenario
    IValues Values { get; }
    T Scenario<T>() where T : class, new();   // made on first use, dropped when the scenario ends

    // Things a mod's plugin gave the harness, such as its window driver
    T Get<T>() where T : class;
}
```

- `Fail` doesn't stop `Run`, so a step that can't go on does `yield break`, or throws `StepFailedException`. The first failure is the one in the results, and `Pass` doesn't take it back.
- A wait that gives up throws `StepFailedException`, so nothing after it runs.
- `Get<T>` fails the step if no plugin gave a service of that type. Two plugins giving one type is an error that fails the run, naming both.

`Scenario<T>()` replaces fields like `PlacedBlockNames` and `RememberedWindowRect` that ZLS's `StepContext` has today. Each mod keeps its own state class instead of growing the shared context.

### Input

```csharp
public interface IInput
{
    InputRoute Route { get; set; }   // per scenario; `input: os` in the YAML header, or a step that switches it

    IEnumerator Press(Keys keys);    // in one frame, as `press` today
    IEnumerator Type(string text);   // one key a frame
    IEnumerator Hold(Keys keys, Seconds duration);
    IEnumerator PressAction(string action);   // the game's input action by its Rewired name, such as EditorUndo
    IEnumerator HoldAction(string action, Seconds duration, float value = 1);   // -1 to 1 for an axis, such as Horizontal
    IEnumerator MoveMouse(Vector2 screenPoint);
    IEnumerator Click(Vector2 screenPoint, MouseButton button = MouseButton.Left);
    IEnumerator Drag(Vector2 from, IReadOnlyList<DragMove> moves, MouseButton button = MouseButton.Left);
    void GiveBack();                 // lets go of what's held and hands back the player's mouse, on every route
}

public enum InputRoute
{
    InProcess,   // Imui events + Harmony on Input + Rewired action fields + uGUI pointer events: same frame, no focus needed
    Os,          // SendInput: what a player's keyboard and mouse do; needs the window in front
}
```

- One `IInput` lives for the whole run. The runner sets `Route` from a scenario's header as it starts, `InProcess` when the header leaves it out, so a route a step switched to ends with its scenario.
- `TesterInput` decides what goes in which frame, and hands it to an `IInputDriver`. The `InProcess` route is a composite of one driver per input path, which all get the same input, and it only fails a step when none of them has anything to take it. Imui's is the first; #16, #17 and #18 add the others.
- The game's input actions aren't keys, so they don't go through that composite: `PressAction` and `HoldAction` hand an `IActionDriver` an action by its Rewired name (`EditorUndo`, `Horizontal`, `Break`, …), whatever keys it's bound to. The game reads each action from `InputActionScriptableObject`s, which it fills from Rewired's events in Rewired's update, the first `Update` of a frame (execution order -32000). The Rewired driver writes them in a Harmony postfix on that update, so every reader sees them in the same frame, with no patch per reader. An action goes down in the frame its value leaves 0 and up in the frame it comes back, as the game's handlers set the fields for a key, and is cleared the frame after, where the game would leave it up until the next event. Until then what the driver writes wins over the player's own input for that action. Every field with the action's id gets it, each as its own `UseAsButton` and `UseNegativeInput` read it. They're in-process only: on `input: os` they fail the step, since a key there reaches an action only through what it's bound to.
- A key is a key event, then the character it types as an event of its own, as Unity hands them to Imui. Ctrl, Alt or Cmd with a key types nothing. `press` lets go in the frame after, and `type` as it sends the next key. The Escapes in one `press` come in one frame, as a quick double press does, because Unity reports one Escape a frame however often it was pressed.
- Unity's own `Input` never sees what the Imui driver sends, so code that reads Escape or anything else there, the level editor included, doesn't see it until the legacy `Input` driver (#16).
- A point, and a `DragMove`'s pixels, are in Unity's screen pixels, with a point counted from the bottom left. The Imui driver turns them into Imui's own pixels with its backend's scale, so a step that got its point from an `ImRect` scales it back first.
- The mouse stays where a call left it until `GiveBack`, so a step can check what's under it. `GiveBack` can't fail: it works with nothing held, on any route and without an Imui backend, so the runner can call it after any scenario (#62).
- Until the OS driver (#19) exists, `input: os` loads and `ztest check` accepts it, and every `IInput` call that sends input on that route fails its step, saying so. `Route` and `GiveBack` still work there, and a scenario on that route with no input step passes.

Core steps (`press`, `type`, `hold`, `click`, `move-mouse`, `drag`) are thin wrappers over this. A mod's step uses it the same way: ZLS's `hover` and `drag-window-edge` would find where to point from its window driver, then call `context.Input`.

## Nested arguments: `drag-window-edge`

Today it's `drag-window-edge -1000 over 10 then 200 over 2`, read by a hand-written parser. In the registry:

```csharp
/// <summary>One move of a drag, in even steps over the frames.</summary>
public sealed class DragMove
{
    /// <summary>Pixels down, or up for a number below 0. With Sideways, pixels right, or left below 0.</summary>
    [Arg(Primary = true)] public Pixels By { get; set; }

    /// <summary>Frames the move takes.</summary>
    [Arg(Default = 1), InRange(1, 600)] public int Over { get; set; } = 1;

    /// <summary>Whether it goes right or left, instead of down or up.</summary>
    [Arg(Default = false)] public bool Sideways { get; set; }
}

/// <summary>Drags the window's resize handle with the mouse, the way a player does …</summary>
[Step("drag-window-edge", Namespace = "zls")]
public sealed class DragWindowEdge : Step
{
    /// <summary>The moves, made in turn before letting go.</summary>
    [Arg(Primary = true), MinItems(1)] public List<DragMove> Moves { get; set; }

    [Arg(Default = false)] public bool Sideways { get; set; }   // replaces drag-window-edge-sideways, setting each move's

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

`Tester`, `TesterInfo`, `IScenarioHooks` and the contexts are in the SDK, so a mod only needs the plugin to name a state it requires. `IRunContext` has `Logger` and `Get<T>()`, `IScenarioContext` adds `ScenarioName` and `Scenario<T>()`, and `IStepContext` adds the rest. A hook runs before the walk to where its scenario starts, so `BeforeScenario` finds the game where the last scenario left it. `RunStarted` runs once before the first walk.

Step classes are found by scanning each `[TestPlugin]` assembly for `[Step]`, in the game from the loaded plugins and offline by the CLI. `Tester.Register` only gives what steps run against. `Namespace` defaults to the `[TestPlugin]` one.

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

A state class needs a public constructor that takes nothing, because the harness makes one to call `IsMet`. Its namespace works as a step's does, including the empty one being reserved for the tester's own.

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

`[Requires]` takes several states, all of which must hold. For "either", an `[RequiresAny(typeof(A), typeof(B))]` form covers it. Each of these attributes can be written more than once, and each `[RequiresAny]` is met separately.

Descriptors record all of this by each state's full name: `StepDescriptor.Requires`, `Enters` and `Leaves`, where each entry of `Requires` is a list of alternatives, and `StepRegistry.States` with each state's `Within`, `Hint` and docs. A step or state that names a type with no `[State]`, or a cycle in `Within`, is a `StepDefinitionException`.

### Checked in two places

- **In the game, before `Run`:** the harness calls `IsMet` for each required state and for the states it's within, outermost first, and on a miss fails the step without running it: `zls.compile needs zls.script-selected: Add zls.new-script or zls.select-script first.` With the window closed that names `zls.window-open` instead, the state to get into first, even if `ScriptSelected.IsMet` still says yes. When none of a `[RequiresAny]` holds, it names each one: `needs a or b: <a's hint> <b's hint>`. The hint's backticks, there for the step reference, are left out of the message. That replaces the hand-written `Require…` calls and gives every step the same message. `Run` can still check things a state can't express, such as whether a named Script exists.
- **Offline, in `ztest check`:**
  - It follows the scenario from its `start:` state, through each step's `Enters` and `Leaves`. A step whose required state doesn't hold at that point is reported with its line and the hint. `start:` holds the state with the name it gives, and a header without `start:` starts in `editor`.
  - `run:` sub-scenarios are followed into, from the state of the step before them. A `start:` in a `run:` file isn't followed.
  - A `for` or `repeat` body is checked once, then again starting from the state it ends in, so a body that closes the window it needs is caught. A `for` is followed once per value, and a `repeat` of at least 2 twice. A body that only runs once is only checked once.
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

### Where a scenario starts

`StartState` lists what `start:` can name, each with its one argument. `StartWalk` (#12) takes the game there the way a player gets there, always through the main menu, so each scenario gets a new menu, level or round whatever the last one left behind. The scenario runner calls it for each scenario:

| `start:` | From the main menu | State it holds |
| --- | --- | --- |
| `main-menu` | Nothing. | `main-menu` |
| `editor`, or no `start:` | The Level Editor button, for a new level. | `editor` |
| `editor: { level: file }` | A new level, then what the load panel does with a file, because ZeepSDK has no way to do this. The path is relative to the scenario's file, as `run:` is. | `editor` |
| `free-play: { uid: … }` | `RacingApi.LoadTrackInFreePlayAsync`, then the round starting. The UID has to be a level the game has loaded: a local, workshop or adventure one. | `free-play` |

- **To the main menu:** the first walk of a run goes from the start screen and skips the one-time panels for that session, without changing settings. Later, the main menu loads again, test mode quits to the editor through the pause menu, the editor quits without saving, and free play quits through the pause menu to the level select or the adventure map, and goes home from there. The game still writes an autosave when the editor quits, which the runner has to put back (#23).
- **Failing:** each wait gives up after 120 seconds, and every failure is a `StartFailedException` saying what wasn't reached, so a changed menu fails the run instead of hanging it. Unlike a failed step, this leaves the game somewhere unknown, so it ends the run (see *Running scenarios*). A level file with no blocks in it fails too, because the game opens a file that isn't a level as an empty one, and `start: editor` is the way to an empty level. A damaged level still opens with the blocks the game could read, and only the game's own warning says so.
- **The states** are `InMainMenu`, `InEditor` and `InFreePlay`, public in the plugin so a mod's step can require them. The walk checks it ended up there. `ztest check` holds the state with the name of the start, and reports a level file that isn't there. A start state no step needs or enters isn't a smell for `ztest check --registry`.
- `${name}` in a `start:` argument fails at load: no step has run yet to define a name.

### Running scenarios

`run.json` lists the files in order (see [run-files.md](run-files.md)). `ScenarioRunner` first loads each file and the `run:` files it uses, so a mistake fails before a run reaches it: a failed `load` step, and the other files still run. Then for each scenario:

1. `BeforeScenario` of each plugin, except for the first scenario.
2. `StartWalk.Enter`, through `GuardedRoutine`.
3. Each node of `steps:`. For a step: bind it, check its states, run it, and record it.
4. `AfterScenario` of each plugin, also after a failed scenario or a failed start.

What a failure does:

| What failed | What happens |
| --- | --- |
| A step | Its scenario stops with a screenshot, and the run goes on with the next, whose walk gets it a new menu, level or round. |
| The walk to a start, with any exception | A failed `start: <state>` step and a screenshot, then `AfterScenario`, and **the run ends**: each scenario left gets a failed `not run` step saying why. The game is somewhere unknown, so a later scenario's result couldn't be trusted, and each further walk could take 120 seconds per wait to fail the same way. A level file that's wrong also ends the run, which `ztest check` catches before one. |
| `BeforeScenario` or `AfterScenario` | A failed step named after the hook. After `BeforeScenario` the scenario doesn't run. The run goes on, because the game is still where it was. |
| `RunStarted` | A failed step named after the hook, and each scenario gets a failed `not run` step. |
| The right-build or parser checks | No scenario runs. |
| `run.json` names no scenarios | A failed `scenarios` step, because a run that tested nothing shouldn't report a pass. |

The harness gives back the mouse after each scenario once there's an input driver to give it back (#15).

The steps the tester ships with so far:

| Step | What it does | Needs | Changes |
| --- | --- | --- | --- |
| `wait` | Waits. | | |
| `log` | Writes a line to the results and the log. | | |
| `screenshot` | Saves what's on screen, and fails if it's blank. | | |
| `expect-blank-screenshot` | Covers the frame in one color and checks the screenshot is found blank. | | |
| `expect-no-exceptions` | Checks the game logged none since the scenario started or this last ran. | | |
| `place-start` | Places a start block below the camera, as one undo step. | `editor` | |
| `place-block` | Places a block of a type a cell to the side and selects it. `as:` names it. | `editor` | |
| `set-block-setting` | Changes a named block's setting through the block's editor, as one undo step. | `editor` | |
| `expect-block-count` | Counts the level's blocks, or with `type:` those of one type. | `editor` | |
| `undo`, `redo` | Press the editor's key once, and fail if the history didn't move. | `editor` | |
| `test-mode` | Starts test mode and waits for the round. The level needs a start block. | `editor` | leaves `editor`, enters `test-mode` |
| `back-to-editor` | Quits through the pause menu and waits for the level to load. | `test-mode` | leaves `test-mode`, enters `editor` |

Input steps come with #20. `Zeepkist.Tester.Tests.InGame` is a test plugin and scenarios for the in-game check, and its `run.sh` deploys and runs them.

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
- Loading a scenario checks every value without a `${…}`, in the game and in `ztest check`, so `wait: soon` fails with its `file:line:col` before a run reaches it. It also tries each `for` value against the type of each argument its variable is used in, because the values are literals. It can't know a step's value from `as:` or a value from `with:`, so a bad one fails when the step runs, with a message giving the argument and the value.

**Each pass of a body has its own scope.** A name defined in a `for` or `repeat` body lasts until that pass ends, and the next pass starts fresh:

```yaml
- repeat: 3
  steps:
    - place-block: { type: 2448, as: lamp }
    - set-block-setting: { block: lamp, setting: is, value: "true" }
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
| Offline checks | `ztest check [files or folders...] [--plugin <dll>]...`, `ztest check --registry` | The registry; reports `file:line:col: error\|warning: message`, and exits with 1 on errors |
| `.zlstest` → YAML | `ztest convert` | The registry plus a `[LineSyntax("<function> [as <as>]")]` attribute per old step; see below |

`ztest check` with no files checks the `scenarios` folder of the nearest `zeepkist-test.json`, and reads the steps of its `testPlugin`, a project or a DLL, unless `--plugin` names others. Of the files it's given, one that another runs is only followed from there, because it may need what that passes in through `with:`. It reports a file's first problem from loading, and then every problem with names and states. It stops following a scenario, with a warning, after a million steps of nested loops and `run:` files, and a value used in nested `for`s tries its first 1000 combinations.

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
