using System;
using System.Collections;
using TNRD.Zeepkist.Tester.Sdk;
using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>Logs an exception as the game would log an uncaught one, for a check to find.</summary>
[Step("log-exception")]
public sealed class LogException : Step
{
    /// <summary>The exception's message.</summary>
    [Arg(Primary = true)]
    public required string Message { get; set; }

    public override IEnumerator Run(IStepContext context)
    {
        Debug.LogException(new InvalidOperationException(Message));
        yield break;
    }
}
