using UnityEngine;

[CreateAssetMenu(fileName = "NewCentipedeConfig", menuName = "Centipede/Config")]
public class CentipedeConfig : ScriptableObject
{
    [Tooltip("Total number of nodes including the head")]
    [Min(1)]
    public int nodeCount = 5;

    [Tooltip("Follow distance between each node (universal)")]
    [Min(0.01f)]
    public float followDistance = 0.3f;

    [Tooltip("World-unit radius for every node's visual and collider")]
    [Min(0.01f)]
    public float nodeRadius = 0.15f;

    [Tooltip("Color applied to every node's SpriteRenderer")]
    public Color nodeColor = Color.white;

    [Header("Spring — Wiggle")]
    [Tooltip("Natural frequency ω (rad/s); higher = balls track skeleton tightly")]
    [Min(0.01f)]
    public float wiggleFrequency = 8.94f;

    [Tooltip("Damping ratio ζ; lower = more wobble after impacts")]
    [Min(0f)]
    public float wiggleDampingRatio = 0.28f;

    [Tooltip("Spring simulation mass; affects detachment energy threshold")]
    [Min(0.01f)]
    public float wiggleMass = 1f;

    public float WiggleStiffness => SpringParams.ComputeStiffness(wiggleFrequency, wiggleMass);
    public float WiggleDamping   => SpringParams.ComputeDamping(wiggleFrequency, wiggleDampingRatio, wiggleMass);

    [Header("Ball Type")]
    [Tooltip("BallDefinition used for every node's visual. Falls back to assembler default if null.")]
    public BallDefinition ballDefinition;

    [Header("Destruction")]
    [Tooltip("Distance a Ball must reach from its SkeletonNode to trigger detachment")]
    [Min(0.01f)]
    public float detachDistance = 0.5f;

    [Header("Navigation")]
    [Tooltip("Movement speed in world units/sec")]
    [Min(0.01f)]
    public float speed = 3f;

    [Tooltip("Seconds before another collision response can trigger")]
    [Min(0f)]
    public float collisionCooldownDuration = 0.5f;

    [Header("Scent Field")]
    [Tooltip("Ring buffer capacity; 200 × 0.1 s interval = 20 seconds of scent trail")]
    [Min(10)]
    public int scentHistorySize = 200;

    [Tooltip("Seconds between player position samples pushed into the scent field")]
    [Min(0.01f)]
    public float scentSampleInterval = 0.1f;

    [Tooltip("Time constant (seconds) for scent weight to decay to 37% of original strength")]
    [Min(0.1f)]
    public float scentDecayTime = 8f;

    [Tooltip("Gaussian spatial spread of each scent sample in world units; set to ~half the expected engagement distance")]
    [Min(0.1f)]
    public float scentSigma = 1.5f;

    [Tooltip("Radius at which 8 gradient samples are taken around the head to estimate field direction")]
    [Min(0.05f)]
    public float scentGradientSampleRadius = 0.8f;

    [Tooltip("Turn-rate factor: how aggressively the centipede blends toward the gradient per second at full sensitivity")]
    [Min(0f)]
    public float scentSteeringBlend = 4f;

    [Tooltip("World-unit radius within which passing suppresses nearby scent samples")]
    [Min(0f)]
    public float scentConsumeRadius = 0.6f;

    [Tooltip("Weight consumed per second at the centipede's center; controls how quickly the spiral tightens")]
    [Min(0f)]
    public float scentConsumeRate = 2f;

    [Tooltip("Sweep-and-lock oscillator frequency in Hz; centipede sweeps ballistically at low phase, snaps at high phase")]
    [Min(0f)]
    public float scentOscillationFrequency = 0.35f;

    [Tooltip("Max speed bonus (world units/sec) when the centipede is on a hot trail directly ahead")]
    [Min(0f)]
    public float scentSpeedBoost = 1f;

    [Tooltip("Forward scent field strength that yields full speed boost; normalize against your typical sigma/historySize")]
    [Min(0.001f)]
    public float scentGradientMaxStrength = 5f;

    [Tooltip("Field strength at the head below which fallback direct pursuit activates")]
    [Min(0f)]
    public float scentFallbackThreshold = 0.05f;

    [Tooltip("Blend rate toward the player during fallback (weaker than gradient steering to avoid snapping)")]
    [Min(0f)]
    public float scentFallbackBlend = 0.8f;

    [Header("Pincers")]
    [Tooltip("Sprite used for both pincer renderers. Right side is flipped. Leave null to disable pincers.")]
    public Sprite pincerSprite;

    [Tooltip("Uniform local scale of each pincer sprite GO. try 0.2–0.8; lower = subtle, higher = dramatic")]
    [Min(0.01f)]
    public float pincerSize = 0.4f;

    [Tooltip("Local X distance from head center to each pincer's pivot point in world units. try 0.05–0.25")]
    [Min(0f)]
    public float pincerOffsetX = 0.12f;

    [Tooltip("Local Y offset from head center to each pincer's pivot point in world units. try -0.1–0.2")]
    public float pincerOffsetY = 0.1f;

    [Tooltip("Click frequency in Hz while player is outside attack radius. try 0.5–3.0; lower = lazy, higher = restless")]
    [Min(0.01f)]
    public float idleClickSpeed = 1.5f;

    [Tooltip("Click frequency in Hz when player is at inner attack radius. try 2.0–8.0")]
    [Min(0.01f)]
    public float attackClickSpeed = 4.0f;

    [Tooltip("Max rotation degrees each pincer pivots from center. try 15–60; lower = tight snip, higher = wide bite")]
    [Min(0f)]
    public float clickAngle = 35f;

    [Tooltip("Distance at which pincers begin speeding up. try 2.0–6.0")]
    [Min(0f)]
    public float attackOuterRadius = 3.0f;

    [Tooltip("Distance at which pincers reach full attackClickSpeed. Must be < attackOuterRadius. try 0.5–2.0")]
    [Min(0f)]
    public float attackInnerRadius = 1.0f;

    [Tooltip("Width x Height of each trigger hitbox in world units. Intentionally smaller than the visual. try (0.05–0.15, 0.10–0.25)")]
    public Vector2 pincerColliderSize = new Vector2(0.1f, 0.15f);

    [Tooltip("Local X offset of each hitbox from head center in world units. try 0.05–0.2")]
    [Min(0f)]
    public float pincerHitboxOffsetX = 0.1f;

    [Tooltip("Local Y offset of each hitbox from head center in world units. try 0.0–0.2")]
    public float pincerHitboxOffsetY = 0.1f;

    [Header("Prefab Overrides")]
    [Tooltip("Override head prefab (falls back to assembler default if null)")]
    public GameObject headPrefab;

    [Tooltip("Override body prefab (falls back to assembler default if null)")]
    public GameObject bodyPrefab;
}
