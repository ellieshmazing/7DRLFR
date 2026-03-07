#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attached to the Arm GameObject. Fires Ball projectiles from the barrel tip
/// on left-click, subject to a per-shot cooldown.
///
/// Each projectile starts at <see cref="initialScale"/> and grows to its true
/// diameter via <see cref="ProjectileScaleGrow"/>, giving the illusion that it
/// emerges from the barrel.
///
/// The TEMPORARY random-diameter path is isolated in <see cref="GetProjectileDiameter"/>
/// and its associated inspector fields. Replace that method (and remove the
/// TEMPORARY header block) once a proper projectile queue is plumbed in.
/// </summary>
public sealed class ProjectileGun : MonoBehaviour
{
    [Tooltip("World-space spawn point at the barrel tip. Assigned by PlayerAssembler.")]
    public Transform? firingPoint;

    [Tooltip("BallDefinition used for every fired projectile.")]
    public BallDefinition? projectileDef;

    [Tooltip("Starting world-unit diameter at spawn — creates the barrel-emergence illusion.")]
    [Min(0.001f)] public float initialScale = 0.05f;

    [Tooltip("Seconds for the projectile to grow from spawn scale to its true diameter.")]
    [Min(0.001f)] public float growTime = 0.15f;

    [Tooltip("World units per second of the fired projectile.")]
    [Min(0.1f)] public float firingSpeed = 10f;

    [Tooltip("Minimum seconds between shots.")]
    [Min(0f)] public float fireCooldown = 0.25f;

    // ── TEMPORARY — Replace with projectile queue ─────────────────────────────
    [Header("TEMPORARY — Remove when projectile queue is implemented")]
    [Min(0.01f)] public float tempMinDiameter = 0.2f;
    [Min(0.01f)] public float tempMaxDiameter = 0.8f;
    // ─────────────────────────────────────────────────────────────────────────

    private float lastFireTime = float.NegativeInfinity;

    private void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (Time.time - lastFireTime < fireCooldown) return;

        FireProjectile();
        lastFireTime = Time.time;
    }

    private void FireProjectile()
    {
        if (firingPoint == null || projectileDef == null) return;

        float diameter = GetProjectileDiameter();

        var projectileGO = new GameObject("Projectile");
        projectileGO.transform.position = firingPoint.position;

        var ball = projectileGO.AddComponent<Ball>();
        ball.Init(projectileDef, diameter, centipedeMode: false);

        // Convert world-unit diameters → localScale values that match the sprite's PPU.
        float targetLocalScale  = Ball.LocalScaleForDiameter(projectileDef.sprite, diameter);
        float initialLocalScale = Ball.LocalScaleForDiameter(projectileDef.sprite, initialScale);
        projectileGO.transform.localScale = Vector3.one * initialLocalScale;

        var scaleGrow = projectileGO.AddComponent<ProjectileScaleGrow>();
        scaleGrow.Initialize(targetLocalScale, growTime);

        ball.Detach((Vector2)transform.right * firingSpeed);
    }

    /// <summary>TEMPORARY — Replace with projectile queue implementation.</summary>
    private float GetProjectileDiameter() =>
        Random.Range(tempMinDiameter, tempMaxDiameter);
}
