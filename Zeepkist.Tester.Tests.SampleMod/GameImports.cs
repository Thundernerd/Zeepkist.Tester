#pragma warning disable IDE0005 // The imports are what this file tests: a name that clashes with one of them doesn't compile
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Rewired;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;
using UnityEngine.UI;
#pragma warning restore IDE0005

namespace TNRD.Zeepkist.Tester.Tests.SampleMod;

/// <summary>Uses the SDK's names next to the namespaces a step file is likely to import.</summary>
[Step("game-imports")]
public sealed class GameImports : Step
{
    [Arg]
    [InRange(1, 8)]
    public int Count { get; set; }

    [Arg]
    public TextBlock Script { get; set; }

    [Arg]
    [Pattern("^[a-z]+$")]
    public string Word { get; set; }

    [Arg]
    [MinItems(1)]
    public List<Seconds> Times { get; set; }

    [Arg]
    public Keys Keys { get; set; }

    [Arg]
    public Pixels Offset { get; set; }

    [Arg]
    public FileName Screenshot { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        yield break;
    }
}
