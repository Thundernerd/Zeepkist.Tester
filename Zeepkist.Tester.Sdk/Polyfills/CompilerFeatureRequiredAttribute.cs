using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// The compiler puts this on the constructors of a type with <c>required</c> properties.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CompilerFeatureRequiredAttribute : Attribute
{
    public const string RefStructs = nameof(RefStructs);
    public const string RequiredMembers = nameof(RequiredMembers);

    public CompilerFeatureRequiredAttribute(string featureName)
    {
        FeatureName = featureName;
    }

    public string FeatureName { get; }

    public bool IsOptional { get; set; }
}
