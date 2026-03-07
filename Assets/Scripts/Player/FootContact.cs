using UnityEngine;

/// <summary>
/// Tracks whether a foot visual is currently in contact with any collider.
/// Uses an enter/exit counter so simultaneous multi-contact (e.g. a foot
/// spanning a seam between two colliders) does not falsely report air.
///
/// Attach alongside CircleCollider2D on each foot visual GO.
/// Queried by PlayerSkeletonRoot to gate jump input.
/// </summary>
public class FootContact : MonoBehaviour
{
    public bool    isGrounded       => _contactCount > 0;
    public Vector2 lastContactNormal { get; private set; }

    private int _contactCount;

    void OnCollisionEnter2D(Collision2D col)
    {
        _contactCount++;
        lastContactNormal = col.GetContact(0).normal;
    }

    void OnCollisionStay2D(Collision2D col)
    {
        lastContactNormal = col.GetContact(0).normal;
    }

    void OnCollisionExit2D(Collision2D _) => _contactCount = Mathf.Max(0, _contactCount - 1);
}
