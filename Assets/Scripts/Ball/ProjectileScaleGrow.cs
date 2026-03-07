#nullable enable
using UnityEngine;

/// <summary>
/// Linearly grows the host GameObject's uniform localScale from its spawn value
/// to <see cref="targetScale"/> over <see cref="growTime"/> seconds, then
/// removes itself. The rate is computed from the scale and time so every
/// projectile reaches full size in the same duration regardless of its size.
///
/// Added by <see cref="ProjectileGun"/> immediately after spawning a projectile
/// so the ball appears to emerge from the barrel at tiny scale and expand to its
/// true size in flight.
/// </summary>
public sealed class ProjectileScaleGrow : MonoBehaviour
{
    private float targetScale;
    private float growthRate;

    /// <summary>
    /// Must be called immediately after AddComponent.
    /// <paramref name="targetScale"/> is a localScale value (not a world-unit diameter);
    /// use <see cref="Ball.LocalScaleForDiameter"/> to convert.
    /// </summary>
    public void Initialize(float targetScale, float growTime)
    {
        this.targetScale = targetScale;
        this.growthRate  = growTime > 0f ? targetScale / growTime : float.MaxValue;
    }

    private void Update()
    {
        float current = transform.localScale.x;

        if (current >= targetScale)
        {
            transform.localScale = Vector3.one * targetScale;
            Destroy(this);
            return;
        }

        transform.localScale = Vector3.one *
            Mathf.MoveTowards(current, targetScale, growthRate * Time.deltaTime);
    }
}
