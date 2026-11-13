using UnityEngine;

namespace TNRD.Zeepkist.Tester.Tests.InGame;

/// <summary>
/// Keeps the furthest the soapbox it's on steered, each frame, so a step after the one that steered can check it.
/// </summary>
public sealed class SteeringWatch : MonoBehaviour
{
    private New_ControlCar _car;

    /// <summary>The furthest it steered, below 0 for left.</summary>
    public float Most { get; private set; }

    private void Awake()
    {
        _car = GetComponent<New_ControlCar>();
    }

    private void Update()
    {
        if (_car != null && Mathf.Abs(_car.lerpedSteering) > Mathf.Abs(Most))
            Most = _car.lerpedSteering;
    }
}
