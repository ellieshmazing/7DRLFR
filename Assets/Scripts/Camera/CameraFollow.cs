using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smooth follow camera. Attach to your scene's Camera object.
/// Reads smoothing and offset values from a CameraConfig asset.
/// Falls back to Inspector values if no config is assigned.
///
/// Target priority:
///   1. PlayerRegistry (auto-populated when a player is spawned/destroyed)
///   2. Mouse world position (when no player exists)
/// The Inspector "target" field can still override this at edit-time,
/// but will be replaced at runtime if a player is registered.
/// </summary>
public class SmoothFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Optional manual override. At runtime, PlayerRegistry takes precedence if a player exists.")]
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

    private void Awake()
    {
        PlayerRegistry.OnPlayerChanged += OnPlayerChanged;

        // Sync with any player that already exists (e.g. spawned before this camera)
        if (PlayerRegistry.PlayerTransform != null)
            target = PlayerRegistry.PlayerTransform;
    }

    private void OnDestroy()
    {
        PlayerRegistry.OnPlayerChanged -= OnPlayerChanged;
    }

    private void OnPlayerChanged(Transform playerTransform)
    {
        target = playerTransform; // null when player is destroyed → falls back to mouse
    }

    private void Start()
    {
        if (config != null)
        {
            _smoothness      = config.smoothing;
            _positionOffset  = config.positionOffset;
            _rotationOffset  = config.rotationOffset;
        }
        else
        {
            Debug.LogWarning("[SmoothFollowCamera] No CameraConfig assigned — using Inspector fallback values.");
            _smoothness     = smoothness;
            _positionOffset = positionOffset;
            _rotationOffset = rotationOffset;
        }
    }

    private void LateUpdate()
    {
        Vector3 followPos = GetFollowPosition();

        Vector3 desiredPosition = followPos + _positionOffset;

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

    private Vector3 GetFollowPosition()
    {
        if (target != null)
            return target.position;

        // No player — follow mouse cursor in world space
        if (Mouse.current != null)
        {
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector3 mouseWorld  = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, Mathf.Abs(Camera.main.transform.position.z)));
            mouseWorld.z = 0f;
            return mouseWorld;
        }

        return transform.position; // stationary fallback
    }
}
