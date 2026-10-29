using UnityEngine;
using UnityEngine.InputSystem;

/// First-person camera for the local player's seat. Hold the right mouse button to look around;
/// on release the view eases back to the table so screen-space gestures always see the same framing.
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

    private float _yaw, _pitch;

    /// Snaps to the resting view (used when building the scene and for previews).
    public void ApplyImmediate()
    {
        _yaw = _pitch = 0f;
        transform.SetPositionAndRotation(_eyePosition, RestRotation);
    }

    private Quaternion RestRotation => Quaternion.LookRotation(_lookTarget - _eyePosition, Vector3.up);

    private void LateUpdate()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
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

        transform.SetPositionAndRotation(
            _eyePosition,
            Quaternion.AngleAxis(_yaw, Vector3.up) * RestRotation * Quaternion.AngleAxis(_pitch, Vector3.right));
    }
}
