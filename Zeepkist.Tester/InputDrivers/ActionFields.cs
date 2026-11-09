namespace TNRD.Zeepkist.Tester.InputDrivers;

/// <summary>
/// What the game reads an input action from, one for each of <see cref="InputActionScriptableObject"/>'s fields.
/// </summary>
internal struct ActionFields
{
    public bool ButtonDown { get; set; }

    public bool ButtonUp { get; set; }

    public bool ButtonHeld { get; set; }

    public bool NegativeButtonDown { get; set; }

    public bool NegativeButtonUp { get; set; }

    public bool NegativeButtonHeld { get; set; }

    public bool PositiveButtonDown { get; set; }

    public bool PositiveButtonUp { get; set; }

    public bool PositiveButtonHeld { get; set; }

    public float Axis { get; set; }
}
