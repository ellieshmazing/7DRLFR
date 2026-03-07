#nullable enable
using UnityEngine;

/// <summary>
/// Linearly grows the host GameObject's uniform localScale from its spawn value
/// to <see cref="targetDiameter"/> over <see cref="growTime"/> seconds, then
/// removes itself. The rate is computed from the diameter and time so every
/// projectile reaches full size in the same duration regardless of its size.
///
/// Added by <see cref="ProjectileGun"/> immediately after spawning a projectile
/// so the ball appears to emerge from the barrel at tiny scale and expand to its
/// true diameter in flight.
/// </summary>
public sealed class ProjectileScaleGrow : MonoBehaviour
{
    private float targetDiameter;
    private float growthRate;

    /// <summary>Must be called immediately after AddComponent.</summary>
    public void Initialize(float targetDiameter, float growTime)
    {
        this.targetDiameter = targetDiameter;
        this.growthRate     = growTime > 0f ? targetDiameter / growTime : float.MaxValue;
    }

    private void Update()
    {
        float current = transform.localScale.x;

        if (current >= targetDiameter)
        {
            transform.localScale = Vector3.one * targetDiameter;
            Destroy(this);
            return;
        }

        transform.localScale = Vector3.one *
            Mathf.MoveTowards(current, targetDiameter, growthRate * Time.deltaTime);
    }
}
