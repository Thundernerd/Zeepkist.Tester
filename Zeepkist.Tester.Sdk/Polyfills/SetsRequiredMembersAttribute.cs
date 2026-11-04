namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Marks a constructor that sets all <c>required</c> properties, so callers don't have to.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class SetsRequiredMembersAttribute : Attribute
{
}
