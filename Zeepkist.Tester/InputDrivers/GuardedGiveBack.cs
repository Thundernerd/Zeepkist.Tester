using System;

namespace TNRD.Zeepkist.Tester.InputDrivers;

internal static class GuardedGiveBack
{
    /// <summary>
    /// Has a driver let go, and hands what it throws, which it shouldn't, to <paramref name="failed"/> instead of the
    /// caller, so a give back can't fail.
    /// </summary>
    public static void Run(Action giveBack, Action<Exception> failed)
    {
        try
        {
            giveBack();
        }
        catch (Exception exception)
        {
            failed(exception);
        }
    }
}
