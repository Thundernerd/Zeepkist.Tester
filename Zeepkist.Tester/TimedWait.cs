using System;
using System.Collections;

namespace TNRD.Zeepkist.Tester;

internal static class TimedWait
{
    /// <summary>
    /// Waits a frame at a time until <paramref name="condition"/> holds, so a changed menu fails the run instead of hanging it.
    /// </summary>
    /// <exception cref="StartFailedException">It didn't hold within <paramref name="seconds"/>.</exception>
    public static IEnumerator Until(Func<bool> condition, string what, float seconds, Func<float> clock)
    {
        var giveUpAt = clock() + seconds;
        while (!condition())
        {
            if (clock() > giveUpAt)
                throw new StartFailedException($"{what}: not reached after {seconds} seconds");

            yield return null;
        }
    }
}
