using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug utility: drives a Rigidbody2D toward the mouse position.
/// Attach to the centipede head (same object as SkeletonRoot).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class DebugMouseFollow : MonoBehaviour
{
    [Tooltip("Movement speed in units per second")]
    public float speed = 3f;

    [Tooltip("Stops moving when closer than this to the cursor")]
    public float deadZone = 0.1f;

    private Rigidbody2D rb;
    private Camera mainCam;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;
    }

    void FixedUpdate()
    {
        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorld = mainCam.ScreenToWorldPoint(screenPos);
        Vector2 toMouse = mouseWorld - rb.position;
        float dist = toMouse.magnitude;

        if (dist > deadZone)
            rb.linearVelocity = (toMouse / dist) * speed;
        else
            rb.linearVelocity = Vector2.zero;
    }
}
