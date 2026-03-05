using UnityEngine;

/// <summary>
/// Simulates spring-damper inertia on a Visual child so it wiggles
/// relative to its parent SkeletonNode. Overrides world position in
/// LateUpdate, after the skeleton has been positioned in FixedUpdate.
/// </summary>
public class NodeWiggle : MonoBehaviour
{
    [Tooltip("Spring pull strength — higher = tighter snap-back")]
    public float stiffness = 80f;

    [Tooltip("Oscillation decay — higher = settles faster")]
    public float damping = 5f;

    [Tooltip("Visual weight — higher = more sluggish, heavier feel")]
    public float mass = 1f;

    private Vector2 currentPos;
    private Vector2 velocity;

    void Start()
    {
        currentPos = (Vector2)transform.position;
    }

    void LateUpdate()
    {
        Vector2 anchor = (Vector2)transform.parent.position;
        Vector2 displacement = currentPos - anchor;

        Vector2 springForce = -stiffness * displacement;
        Vector2 dampingForce = -damping * velocity;
        Vector2 acceleration = (springForce + dampingForce) / mass;

        velocity += acceleration * Time.deltaTime;
        currentPos += velocity * Time.deltaTime;

        transform.position = (Vector3)currentPos;
    }
}
