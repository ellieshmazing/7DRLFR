using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Positions and rotates the combined hand+gun sprite on a fixed circular
/// track centred on the torso visual, always aimed toward the mouse cursor.
///
/// The orbit centre follows the torso VISUAL transform (which includes the
/// NodeWiggle offset) so the arm track stays in sync with the wiggling body,
/// exactly as described: "the hands match the torso's movement when it
/// wiggles off of its anchor node."
///
/// Assumes the arm sprite points rightward (0°) at default rotation.
/// Runs in LateUpdate after NodeWiggle has repositioned the torso visual.
/// </summary>
[DefaultExecutionOrder(1)]   // after NodeWiggle (order 0) so torso visual is settled
public class PlayerArmController : MonoBehaviour
{
    [Tooltip("The torso visual Transform (set by PlayerAssembler). " +
             "The arm orbits around its world position.")]
    public Transform torsoVisual;

    [Tooltip("Distance from the torso visual centre to the arm sprite pivot")]
    public float orbitRadius = 0.3f;

    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void LateUpdate()
    {
        if (torsoVisual == null || mainCam == null || Mouse.current == null) return;

        // Convert mouse screen position to world space
        Vector2 screenPos  = Mouse.current.position.ReadValue();
        Vector2 mouseWorld = mainCam.ScreenToWorldPoint(screenPos);

        Vector2 torsoPos  = torsoVisual.position;
        Vector2 direction = (mouseWorld - torsoPos).normalized;

        // Lock to circular track
        transform.position = (Vector3)(torsoPos + direction * orbitRadius);

        // Rotate sprite so its right axis points toward the mouse
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
