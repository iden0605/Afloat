using UnityEngine;

/// <summary>
/// Optional per-fish tilt enhancement.
/// Does NOT set localRotation itself — AnchovySwarmAttack owns all rotation.
/// Call TickTilt() each frame before reading CurrentTilt to get the smoothed lean angle.
/// </summary>
public class AnchovyFish : MonoBehaviour
{
    [Tooltip("Max Z-rotation lean when swimming (degrees).")]
    [SerializeField] private float maxTilt = 14f;

    [Tooltip("How quickly the lean blends to the target angle.")]
    [SerializeField] private float tiltLerpSpeed = 8f;

    /// <summary>Current smoothed tilt angle (degrees). Add this to the facing angle in AnchovySwarmAttack.</summary>
    public float CurrentTilt { get; private set; } = 0f;

    private float _targetTilt = 0f;

    /// <summary>Advance the tilt lerp. Call once per frame from AnchovySwarmAttack before reading CurrentTilt.</summary>
    public void TickTilt()
    {
        CurrentTilt = Mathf.LerpAngle(CurrentTilt, _targetTilt, Time.deltaTime * tiltLerpSpeed);
    }

    /// <summary>dirX > 0 = moving right, dirX < 0 = moving left.</summary>
    public void SetTilt(float dirX)
    {
        _targetTilt = -Mathf.Sign(dirX) * maxTilt * Mathf.Clamp01(Mathf.Abs(dirX));
    }

    public void ResetTilt()
    {
        _targetTilt  = 0f;
        CurrentTilt  = 0f;
    }
}
