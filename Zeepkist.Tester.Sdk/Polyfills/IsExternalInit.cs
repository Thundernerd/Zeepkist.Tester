using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets net472 code use <c>init</c> properties. The compiler finds it by name, so it has to be public here.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class IsExternalInit
{
}
