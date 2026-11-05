using System;
using System.Collections;
using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester;

internal static class GuardedRoutine
{
    /// <summary>
    /// Runs <paramref name="routine"/> and the waits it yields itself, and stops at the first exception anywhere in
    /// them, which goes to <paramref name="failed"/>. Unity would only log it and leave the coroutine waiting forever.
    /// </summary>
    public static IEnumerator Run(IEnumerator routine, Action<Exception> failed)
    {
        var running = new Stack<IEnumerator>();
        running.Push(routine);
        while (running.Count > 0)
        {
            object current;
            try
            {
                if (!running.Peek().MoveNext())
                {
                    running.Pop();
                    continue;
                }

                current = running.Peek().Current;
            }
            catch (Exception exception)
            {
                failed(exception);
                yield break;
            }

            if (current is IEnumerator nested)
                running.Push(nested);
            else
                yield return current;
        }
    }
}
