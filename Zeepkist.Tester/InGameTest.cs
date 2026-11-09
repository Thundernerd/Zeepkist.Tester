using System;
using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// One <see cref="InGameTestAttribute"/> method.
/// </summary>
internal sealed class InGameTest
{
    private readonly MethodInfo _method;

    public InGameTest(TestDescriptor descriptor, MethodInfo method)
    {
        Descriptor = descriptor;
        _method = method;
    }

    public TestDescriptor Descriptor { get; }

    /// <summary>
    /// Makes a new instance of the test's class, and calls the test on it.
    /// </summary>
    public IEnumerator Run(IStepContext context)
    {
        try
        {
            return (IEnumerator)_method.Invoke(Activator.CreateInstance(_method.DeclaringType), [context]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
