using UnityEngine;

/// <summary>
/// Enumerated list of Ball types. Each type should have a corresponding BallDefinition asset.
/// To add a new type: add an entry here, create a BallDefinition SO asset, and optionally
/// subclass BallMovementOverride or BallEffect for custom behavior.
/// </summary>
public enum BallType
{
    Standard,
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
/// (e.g. sticky, homing, bouncy).
/// Return true to suppress the default Unity physics for that frame.
/// </summary>
public abstract class BallMovementOverride : ScriptableObject
{
    /// <summary>Called each FixedUpdate for balls that are not in Centipede Mode.</summary>
    /// <returns>True if default physics should be suppressed this frame.</returns>
    public abstract bool OnFixedUpdate(Ball ball, Rigidbody2D rb, float dt);
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstract ScriptableObject base for Ball collision effects.
/// Subclass to implement unique per-type effects (e.g. splash, explosion, stick).
/// Active in both Centipede Mode and free physics mode.
/// </summary>
public abstract class BallEffect : ScriptableObject
{
    /// <summary>Called when this ball enters a collision.</summary>
    public abstract void OnCollision(Ball ball, Collision2D collision);
}
