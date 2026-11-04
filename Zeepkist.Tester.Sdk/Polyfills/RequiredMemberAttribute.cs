using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets a mod's net472 step use <c>required</c> properties. The compiler finds it by name, so it has to be public here.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class RequiredMemberAttribute : Attribute
{
}
