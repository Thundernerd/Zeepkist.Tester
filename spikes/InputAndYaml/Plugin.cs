using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Rewired;
using UnityEngine;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TNRD.Zeepkist.Tester.Spike;

/// <summary>
/// Runs only with -ztest-spike &lt;id&gt; and a run.json holding the same id next to this DLL.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private const string Flag = "-ztest-spike";
    private const float WaitSeconds = 90;

    private readonly Dictionary<string, object> _results = new();
    private readonly List<string> _log = [];
    private string _folder;
    private string _runId;
    private Harmony _harmony;

    // What OnGUI saw, by key, for the probes
    private readonly HashSet<KeyCode> _guiKeyDowns = [];

    private void Awake()
    {
        _folder = Path.GetDirectoryName(Info.Location);
        var args = Environment.GetCommandLineArgs();
        var at = Array.IndexOf(args, Flag);
        var runJson = Path.Combine(_folder, "run.json");
        if (at < 0 || at + 1 >= args.Length || !File.Exists(runJson))
            return;

        _runId = args[at + 1];
        var expected = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(runJson));
        if (expected == null || !expected.TryGetValue("runId", out var id) || id != _runId)
            return;

        Log($"run {_runId} starting, runInBackground {Application.runInBackground}");
        _results["run_in_background"] = Application.runInBackground;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
        _results["yaml"] = YamlProbe();
        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    private void OnGUI()
    {
        var e = Event.current;
        if (e != null && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            _guiKeyDowns.Add(e.keyCode);
    }

    private IEnumerator Run()
    {
        yield return Guarded(Probes());
        Finish();
    }

    /// <summary>
    /// Steps through nested routines itself, because an exception in one that Unity runs never reaches this catch and the results would be lost.
    /// </summary>
    private IEnumerator Guarded(IEnumerator routine)
    {
        var routines = new Stack<IEnumerator>();
        routines.Push(routine);
        while (routines.Count > 0)
        {
            object current;
            try
            {
                if (!routines.Peek().MoveNext())
                {
                    routines.Pop();
                    continue;
                }

                current = routines.Peek().Current;
            }
            catch (Exception exception)
            {
                Log($"exception: {exception}");
                _results["exception"] = exception.ToString();
                yield break;
            }

            if (current is IEnumerator nested)
                routines.Push(nested);
            else
                yield return current;
        }
    }

    private IEnumerator Probes()
    {
        PressAnyKeyToStartGame startScreen = null;
        yield return WaitUntil(() => (startScreen = FindObjectOfType<PressAnyKeyToStartGame>(true)) != null, "start screen object");
        yield return WaitUntil(() => startScreen.disableThisIntroPanel.activeInHierarchy && PlayerManager.Instance != null, "press any key");
        yield return new WaitForSecondsRealtime(1);

        _results["focus"] = FocusProbe();
        Log($"focus: {JsonConvert.SerializeObject(_results["focus"])}");
        _results["display"] = $"{Screen.width}x{Screen.height} {Screen.fullScreenMode}";

        PlayerManager.Instance.modPanelHasAlreadyBeenDisplayed = true;
        PlayerManager.Instance.epilepsyPanelHasAlreadyBeenDisplayed = true;

        var mouse = new Dictionary<string, object>();
        yield return MouseProbe(mouse);
        _results["sendinput_mouse"] = mouse;

        // The start screen advances on any button, F9 too, so it sits out the F9 probes and only Space can advance it
        startScreen.enabled = false;
        var vk = new Dictionary<string, object>();
        yield return KeyProbe(KeyCode.F9, 0x78, 0x43, Native.KeyMode.VirtualKey, vk);
        _results["sendinput_key_vk"] = vk;

        var scan = new Dictionary<string, object>();
        yield return KeyProbe(KeyCode.F9, 0x78, 0x43, Native.KeyMode.ScanCode, scan);
        _results["sendinput_key_scancode"] = scan;

        // The start screen advances on Rewired's GetAnyButtonDown, so this checks the game reacts to it, not just Rewired's API
        startScreen.enabled = true;
        var space = new Dictionary<string, object>();
        space["start_screen_advanced_before"] = startScreen.didit;
        yield return KeyProbe(KeyCode.Space, 0x20, 0x39, Native.KeyMode.Both, space);
        space["start_screen_advanced"] = startScreen.didit;
        _results["sendinput_space_start_screen"] = space;

        if (!startScreen.didit)
        {
            Log("start screen didn't advance from SendInput, advancing it directly");
            startScreen.LoadNextLevel();
        }

        yield return WaitUntil(() => FindObjectOfType<LevelEditorUI>(true) != null, "main menu");
        yield return new WaitForSecondsRealtime(3);

        var harmony = new Dictionary<string, object>();
        yield return HarmonyInputProbe(harmony);
        _results["harmony_input"] = harmony;
    }

    private Dictionary<string, object> FocusProbe()
    {
        var result = new Dictionary<string, object>();
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            result["platform"] = Application.platform.ToString();
            return result;
        }

        var window = Native.GameWindow();
        var foreground = Native.GetForegroundWindow();
        result["game_window_found"] = window != IntPtr.Zero;
        if (window == IntPtr.Zero)
            return result;

        result["game_window_is_foreground"] = window == foreground;
        if (window != foreground)
        {
            result["set_foreground_returned"] = Native.SetForegroundWindow(window);
            result["game_window_is_foreground_after"] = window == Native.GetForegroundWindow();
        }

        return result;
    }

    private IEnumerator MouseProbe(Dictionary<string, object> result)
    {
        var legacyBefore = Input.mousePosition;
        var rewiredBefore = ReInput.controllers.Mouse.screenPosition;
        result["game_window_is_foreground"] = Native.GameWindow() == Native.GetForegroundWindow();
        result["legacy_before"] = Describe(legacyBefore);
        result["rewired_before"] = Describe(rewiredBefore);

        // Two different points, so a cursor that happened to rest at one still shows a move
        var moved = new List<string>();
        foreach (var target in new[] { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.6f) })
        {
            var sent = Native.MoveMouseToWindowFraction(target);
            result[$"sent_{target.x}_{target.y}"] = sent;
            for (var i = 0; i < 5; i++)
                yield return null;

            moved.Add($"legacy {Describe(Input.mousePosition)} rewired {Describe(ReInput.controllers.Mouse.screenPosition)} target {target}");
        }

        result["after_each_move"] = moved;
        result["legacy_moved"] = Input.mousePosition != legacyBefore;
        result["rewired_moved"] = ReInput.controllers.Mouse.screenPosition != rewiredBefore;
    }

    private IEnumerator KeyProbe(KeyCode key, ushort vk, ushort scan, Native.KeyMode mode, Dictionary<string, object> result)
    {
        _guiKeyDowns.Clear();
        bool legacy = false, legacyDown = false, rewired = false, rewiredDown = false, rewiredAny = false;
        result["game_window_is_foreground"] = Native.GameWindow() == Native.GetForegroundWindow();
        result["sent_down"] = Native.SendKey(vk, scan, mode, up: false);
        for (var i = 0; i < 20; i++)
        {
            if (i == 3)
                result["sent_up"] = Native.SendKey(vk, scan, mode, up: true);

            yield return null;
            legacy |= Input.GetKey(key);
            legacyDown |= Input.GetKeyDown(key);
            rewired |= ReInput.controllers.Keyboard.GetKey(key);
            rewiredDown |= ReInput.controllers.Keyboard.GetKeyDown(key);
            rewiredAny |= ReInput.controllers.GetAnyButtonDown();
        }

        result["legacy_input_getkey"] = legacy;
        result["legacy_input_getkeydown"] = legacyDown;
        result["rewired_keyboard_getkey"] = rewired;
        result["rewired_keyboard_getkeydown"] = rewiredDown;
        result["rewired_any_button_down"] = rewiredAny;
        result["ongui_keydown"] = _guiKeyDowns.Contains(key);
        Log($"key {key} {mode}: {JsonConvert.SerializeObject(result)}");
    }

    /// <summary>
    /// PlayerManager.Update toggles showCollisionValues on Input.GetKeyDown(PageUp) with Input.GetKey(LeftAlt), so a
    /// change shows game code going through the patched methods rather than an inlined copy.
    /// </summary>
    private IEnumerator HarmonyInputProbe(Dictionary<string, object> result)
    {
        var before = PlayerManager.Instance.showCollisionValues;
        SyntheticInput.Held.Add(KeyCode.LeftAlt);
        SyntheticInput.DownFrame[KeyCode.PageUp] = Time.frameCount + 1;
        for (var i = 0; i < 4; i++)
            yield return null;

        SyntheticInput.Held.Clear();
        SyntheticInput.DownFrame.Clear();
        var after = PlayerManager.Instance.showCollisionValues;
        result["before"] = before;
        result["after"] = after;
        result["game_code_saw_patched_input"] = before != after;
        PlayerManager.Instance.showCollisionValues = before;
    }

    private Dictionary<string, object> YamlProbe()
    {
        var result = new Dictionary<string, object>();
        try
        {
            var assembly = typeof(YamlStream).Assembly;
            result["assembly"] = assembly.FullName;
            result["location"] = assembly.Location;
            result["loaded_yamldotnet_assemblies"] = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("YamlDotNet", StringComparison.OrdinalIgnoreCase))
                .Select(a => $"{a.FullName} @ {SafeLocation(a)}")
                .ToList();

            const string scenario = """
                description: spike
                steps:
                  - open-window
                  - place-block: { function: lamp, as: lamp }
                  - library-read-failed: on
                  - set-text: |
                      function edge(x) -> rising
                          rising = x
                      end
                  - wait: 2
                """;
            var stream = new YamlStream();
            stream.Load(new StringReader(scenario));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;
            var steps = (YamlSequenceNode)root.Children[new YamlScalarNode("steps")];
            result["step_count"] = steps.Children.Count;
            result["steps"] = steps.Children.Select(step => $"line {step.Start.Line} col {step.Start.Column}: {Describe(step)}").ToList();

            try
            {
                new YamlStream().Load(new StringReader("steps:\n  - wait: 2\n    - broken\n"));
                result["error_probe"] = "no error";
            }
            catch (YamlException exception)
            {
                result["error_probe"] = $"line {exception.Start.Line} col {exception.Start.Column}: {exception.Message}";
            }

            result["ok"] = true;
        }
        catch (Exception exception)
        {
            result["ok"] = false;
            result["error"] = exception.ToString();
        }

        Log($"yaml: {JsonConvert.SerializeObject(result)}");
        return result;
    }

    private static string Describe(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => $"scalar '{scalar.Value}'",
            YamlMappingNode map => "map " + string.Join(", ", map.Children.Select(pair => $"{Describe(pair.Key)} => {Describe(pair.Value).Replace("\n", "\\n")}")),
            _ => node.NodeType.ToString(),
        };
    }

    private static string Describe(Vector2 v) => $"({v.x:0},{v.y:0})";

    private static string Describe(Vector3 v) => $"({v.x:0},{v.y:0})";

    private static string SafeLocation(System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.Location;
        }
        catch
        {
            return "?";
        }
    }

    private IEnumerator WaitUntil(Func<bool> condition, string what)
    {
        var giveUpAt = Time.realtimeSinceStartup + WaitSeconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > giveUpAt)
                throw new TimeoutException($"{what} not reached after {WaitSeconds} s");

            yield return null;
        }
    }

    private void Log(string line)
    {
        _log.Add(line);
        Logger.LogInfo($"[ZTEST-SPIKE] {line}");
    }

    private void Finish()
    {
        _results["log"] = _log;
        _results["done"] = true;
        var outDir = Path.Combine(_folder, "out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "results.json"), JsonConvert.SerializeObject(_results, Formatting.Indented), Encoding.UTF8);
        Log($"run {_runId} done");
        Application.Quit();
    }
}

internal static class SyntheticInput
{
    public static readonly HashSet<KeyCode> Held = [];
    public static readonly Dictionary<KeyCode, int> DownFrame = [];
}

[HarmonyPatch(typeof(Input), nameof(Input.GetKey), typeof(KeyCode))]
internal static class Input_GetKey
{
    private static bool Prefix(KeyCode key, ref bool __result)
    {
        if (!SyntheticInput.Held.Contains(key) && !(SyntheticInput.DownFrame.TryGetValue(key, out var frame) && frame == Time.frameCount))
            return true;

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
internal static class Input_GetKeyDown
{
    private static bool Prefix(KeyCode key, ref bool __result)
    {
        if (!(SyntheticInput.DownFrame.TryGetValue(key, out var frame) && frame == Time.frameCount))
            return true;

        __result = true;
        return false;
    }
}

internal static class Native
{
    public enum KeyMode
    {
        VirtualKey,
        ScanCode,
        Both,
    }

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventAbsolute = 0x8000;
    private const uint MouseEventVirtualDesk = 0x4000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref POINT point);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    /// <summary>
    /// The game's own window. GetActiveWindow can't tell: it returns null while another application is in front.
    /// </summary>
    public static IntPtr GameWindow()
    {
        var self = (uint)Process.GetCurrentProcess().Id;
        var window = IntPtr.Zero;
        while ((window = FindWindowEx(IntPtr.Zero, window, "UnityWndClass", null)) != IntPtr.Zero)
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner == self)
                return window;
        }

        return IntPtr.Zero;
    }

    public static string SendKey(ushort vk, ushort scan, KeyMode mode, bool up)
    {
        var flags = up ? KeyEventKeyUp : 0;
        if (mode == KeyMode.ScanCode)
            flags |= KeyEventScanCode;

        var input = new INPUT
        {
            type = InputKeyboard,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = mode == KeyMode.ScanCode ? (ushort)0 : vk,
                    wScan = mode == KeyMode.VirtualKey ? (ushort)0 : scan,
                    dwFlags = flags,
                },
            },
        };
        return Send(input);
    }

    /// <summary>
    /// Moves the cursor to a fraction of the game window's client area, from its top left.
    /// </summary>
    public static string MoveMouseToWindowFraction(Vector2 fraction)
    {
        var window = GameWindow();
        if (!GetClientRect(window, out var rect))
            return "GetClientRect failed";

        var point = new POINT { x = (int)(rect.right * fraction.x), y = (int)(rect.bottom * fraction.y) };
        ClientToScreen(window, ref point);

        // Absolute coordinates are 0..65535 over the virtual desktop
        var left = GetSystemMetrics(76);
        var top = GetSystemMetrics(77);
        var width = Math.Max(1, GetSystemMetrics(78));
        var height = Math.Max(1, GetSystemMetrics(79));
        var input = new INPUT
        {
            type = InputMouse,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = (int)((point.x - left) * 65535L / width),
                    dy = (int)((point.y - top) * 65535L / height),
                    dwFlags = MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk,
                },
            },
        };
        return $"screen ({point.x},{point.y}) {Send(input)}";
    }

    private static string Send(INPUT input)
    {
        var sent = SendInput(1, [input], Marshal.SizeOf(typeof(INPUT)));
        return sent == 1 ? "sent" : $"SendInput returned {sent}, error {Marshal.GetLastWin32Error()}";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }
}
