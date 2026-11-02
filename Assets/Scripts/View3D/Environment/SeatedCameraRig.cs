using UnityEngine;
using UnityEngine.InputSystem;

/// First-person camera for the local player's seat. Hold the right mouse button to look around;
/// on release the view eases back to the table so screen-space gestures always see the same framing.
/// Interactions can take the camera to a close-up (<see cref="Focus"/>) and hand it back
/// (<see cref="ReleaseFocus"/>); it glides between the two.
[RequireComponent(typeof(Camera))]
public class SeatedCameraRig : MonoBehaviour
{
    [SerializeField] Vector3 _eyePosition = new Vector3(0f, 1.24f, -1.12f);
    [SerializeField] Vector3 _lookTarget  = new Vector3(0f, 0.78f, -0.12f);

    [Header("Look (hold right mouse)")]
    [SerializeField] float _degreesPerPixel = 0.12f;
    [SerializeField] float _maxYaw   = 75f;
    [SerializeField] float _maxPitch = 30f;
    [SerializeField] float _returnSharpness = 4f;

    [Header("Focus")]
    [SerializeField] float _focusSharpness = 6f;

    private float      _yaw, _pitch;
    private bool       _focused;
    private float      _focusBlend; // 0 = seat view, 1 = focus view
    private Vector3    _focusPosition;
    private Quaternion _focusRotation = Quaternion.identity;

    public bool IsFocused => _focused;

    /// Glides to a close-up looking from <paramref name="eye"/> at <paramref name="target"/>.
    /// Free look is disabled until <see cref="ReleaseFocus"/>.
    public void Focus(Vector3 eye, Vector3 target, bool immediate = false)
    {
        _focused       = true;
        _focusPosition = eye;
        _focusRotation = Quaternion.LookRotation(target - eye, Vector3.up);
        if (immediate) ApplyImmediate();
    }

    public void ReleaseFocus() => _focused = false;

    /// Snaps to the current resting view — the seat, or the focus if one is set (scene building, previews).
    public void ApplyImmediate()
    {
        _yaw = _pitch = 0f;
        _focusBlend = _focused ? 1f : 0f;
        Apply();
    }

    private Quaternion RestRotation => Quaternion.LookRotation(_lookTarget - _eyePosition, Vector3.up);

    private void LateUpdate()
    {
        var mouse = Mouse.current;
        if (!_focused && mouse != null && mouse.rightButton.isPressed)
        {
            var delta = mouse.delta.ReadValue();
            _yaw   = Mathf.Clamp(_yaw   + delta.x * _degreesPerPixel, -_maxYaw,   _maxYaw);
            _pitch = Mathf.Clamp(_pitch - delta.y * _degreesPerPixel, -_maxPitch, _maxPitch);
        }
        else
        {
            float k = 1f - Mathf.Exp(-_returnSharpness * Time.deltaTime);
            _yaw   = Mathf.Lerp(_yaw,   0f, k);
            _pitch = Mathf.Lerp(_pitch, 0f, k);
        }

        float f = 1f - Mathf.Exp(-_focusSharpness * Time.deltaTime);
        _focusBlend = Mathf.Lerp(_focusBlend, _focused ? 1f : 0f, f);
        Apply();
    }

    private void Apply()
    {
        var seatRotation = Quaternion.AngleAxis(_yaw, Vector3.up) * RestRotation * Quaternion.AngleAxis(_pitch, Vector3.right);
        float e = _focusBlend * _focusBlend * (3f - 2f * _focusBlend); // smoothstep
        transform.SetPositionAndRotation(
            Vector3.Lerp(_eyePosition, _focusPosition, e),
            Quaternion.Slerp(seatRotation, _focusRotation, e));
    }
}
