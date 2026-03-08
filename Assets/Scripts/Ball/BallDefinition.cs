using UnityEngine;

/// <summary>
/// Enumerated list of Ball types. Each type should have a corresponding BallDefinition asset.
/// To add a new type: add an entry here, create a BallDefinition SO asset, and optionally
/// subclass BallMovementOverride or BallEffect for custom behavior.
/// </summary>
public enum BallType
{
    Standard,
    Fire,
    Sticky,
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Defines the properties of one Ball type: sprite, base mass, and optional
/// movement and collision-effect overrides.
///
/// Create one asset per Ball type via Assets > Create > Ball > Ball Definition.
/// </summary>
[CreateAssetMenu(fileName = "NewBallDefinition", menuName = "Ball/Ball Definition")]
public class BallDefinition : ScriptableObject
{
    [Tooltip("Which Ball type this definition represents")]
    public BallType type;

    [Tooltip("Sprite for this ball type. Author at 1 world-unit diameter at scale 1.")]
    public Sprite sprite;

    [Tooltip("Mass for a ball with diameter 1. Actual mass = baseMass × diameter².")]
    [Min(0.01f)]
    public float baseMass = 1f;

    [Tooltip("Optional movement override. If null, default Unity physics applies (dynamic) " +
             "or centipede spring applies (centipede mode).")]
    public BallMovementOverride movementOverride;

    [Tooltip("Optional collision effect. Active in both Centipede Mode and free physics mode.")]
    public BallEffect effect;
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstract ScriptableObject base for custom Ball movement equations.
/// Subclass to override how a non-Centipede-Mode ball moves each physics step
/// (e.g. homing, boomerang, gravity-well orbit).
///
/// Override <see cref="OnLaunch"/> to initialize per-shot state (e.g. acquire a
/// homing target). Override <see cref="OnFixedUpdate"/> to apply forces or
/// directly set velocity each physics step. To take full control of the body
/// (e.g. make it follow a path), switch it to Kinematic via rb.bodyType inside
/// OnFixedUpdate; remember to switch back to Dynamic if it returns to normal physics.
/// </summary>
public abstract class BallMovementOverride : ScriptableObject
{
    /// <summary>
    /// Called once when the ball transitions to free-physics mode (fired or detached).
    /// Override to initialize per-shot state: lock a homing target, set a timer, etc.
    /// Default implementation does nothing.
    /// </summary>
    public virtual void OnLaunch(Ball ball, Rigidbody2D rb, Vector2 launchVelocity) { }

    /// <summary>Called each FixedUpdate for balls that are not in Centipede Mode.</summary>
    public abstract void OnFixedUpdate(Ball ball, Rigidbody2D rb, float dt);
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstract ScriptableObject base for Ball collision and launch effects.
/// Subclass to implement unique per-type effects (e.g. explosion, fire trail, sticky).
/// Active in both Centipede Mode and free physics mode.
/// </summary>
public abstract class BallEffect : ScriptableObject
{
    /// <summary>
    /// Called once when the ball transitions to free-physics mode (fired or detached).
    /// Override to start a particle trail, play a launch sound, arm a fuse timer, etc.
    /// Default implementation does nothing.
    /// </summary>
    public virtual void OnLaunch(Ball ball, Rigidbody2D rb, Vector2 launchVelocity) { }

    /// <summary>
    /// Called every Update tick while the ball is alive.
    /// Override to drive per-frame logic such as a trail following the ball.
    /// Default implementation does nothing.
    /// </summary>
    public virtual void OnUpdate(Ball ball) { }

    /// <summary>
    /// Called when this ball enters a collision. Active in both Centipede Mode and free mode.
    /// </summary>
    public abstract void OnCollision(Ball ball, Collision2D collision);
}