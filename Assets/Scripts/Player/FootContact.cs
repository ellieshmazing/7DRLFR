using UnityEngine;

/// <summary>
/// Tracks ground and wall contact state for a foot visual.
///
/// Ground contacts use an enter/exit counter for reliability across
/// multi-collider seams. Wall contacts use a per-frame flag set in
/// OnCollisionStay2D and cleared each FixedUpdate — simpler and avoids
/// counter-mismatch bugs at corners where ground and wall meet.
///
/// Attach alongside CircleCollider2D on each foot visual GO.
/// Queried by FootMovement for ground/wall state.
/// </summary>
public class FootContact : MonoBehaviour
{
    public bool    isGrounded        => _contactCount > 0;
    public Vector2 lastContactNormal { get; private set; }
    public bool    isWalled          { get; private set; }
    public Vector2 lastWallNormal    { get; private set; }

    [Tooltip("Angle threshold for wall detection. Surfaces steeper than this " +
             "(relative to Vector2.up) are walls. Matches maxWalkableAngle in PlayerConfig.")]
    public float wallAngleThreshold = 50f;

    private int  _contactCount;
    private bool _walledThisFrame;

    void FixedUpdate()
    {
        // Per-frame wall detection: latch from Stay, reset each physics step
        isWalled = _walledThisFrame;
        _walledThisFrame = false;
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        _contactCount++;
        UpdateNormals(col);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        UpdateNormals(col);
    }

    void OnCollisionExit2D(Collision2D _) => _contactCount = Mathf.Max(0, _contactCount - 1);

    private void UpdateNormals(Collision2D col)
    {
        Vector2 normal = col.GetContact(0).normal;
        float angle = Vector2.Angle(normal, Vector2.up);

        lastContactNormal = normal;

        if (angle > wallAngleThreshold)
        {
            _walledThisFrame = true;
            lastWallNormal   = normal;
        }
    }
}
