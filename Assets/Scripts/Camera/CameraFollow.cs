using UnityEngine;

/// <summary>
/// Smooth follow camera. Attach to your scene's Camera object.
/// Reads smoothing and offset values from a CameraConfig asset.
/// Falls back to Inspector values if no config is assigned.
/// </summary>
public class SmoothFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The transform this camera will follow. Assign your player here.")]
    public Transform target;

    [Header("Config")]
    [Tooltip("CameraConfig asset to read settings from. If null, Inspector fallback values are used.")]
    public CameraConfig config;

    [Header("Fallback (used when Config is null)")]
    [Tooltip("How quickly the camera moves to the new position. Higher = snappier.")]
    public float smoothness = 5f;

    [Tooltip("World-space positional offset from the player.")]
    public Vector3 positionOffset = new Vector3(0f, 3f, -8f);

    [Tooltip("Camera rotation expressed as Euler angles (degrees).")]
    public Vector3 rotationOffset = new Vector3(10f, 0f, 0f);

    // ── Resolved values ───────────────────────────────────────────────────────

    private float _smoothness;
    private Vector3 _positionOffset;
    private Vector3 _rotationOffset;

    // -------------------------------------------------------------------------

    private void Start()
    {
        if (config != null)
        {
            _smoothness = config.smoothing;
            _positionOffset = config.positionOffset;
            _rotationOffset = config.rotationOffset;
        }
        else
        {
            Debug.LogWarning("[SmoothFollowCamera] No CameraConfig assigned — using Inspector fallback values.");
            _smoothness = smoothness;
            _positionOffset = positionOffset;
            _rotationOffset = rotationOffset;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Debug.LogWarning("[SmoothFollowCamera] No target assigned.");
            return;
        }

        Vector3 desiredPosition = target.position + _positionOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            _smoothness * Time.deltaTime
        );

        Quaternion desiredRotation = Quaternion.Euler(_rotationOffset);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            _smoothness * Time.deltaTime
        );
    }
}