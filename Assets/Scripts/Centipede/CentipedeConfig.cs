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

    [Header("Wiggle")]
    [Tooltip("Spring pull strength — higher = tighter snap-back")]
    public float wiggleStiffness = 80f;

    [Tooltip("Oscillation decay — higher = settles faster")]
    public float wiggleDamping = 5f;

    [Tooltip("Visual weight — higher = more sluggish, heavier feel")]
    [Min(0.01f)]
    public float wiggleMass = 1f;

    [Header("Ball Type")]
    [Tooltip("BallDefinition used for every node's visual. Falls back to assembler default if null.")]
    public BallDefinition ballDefinition;

    [Header("Destruction")]
    [Tooltip("Distance a Ball must reach from its SkeletonNode to trigger detachment")]
    [Min(0.01f)]
    public float detachDistance = 0.5f;

    [Header("Pathfinding")]
    [Tooltip("Arc traversal speed in world units/sec")]
    [Min(0.01f)]
    public float speed = 3f;

    [Tooltip("Minimum allowed arc radius; prevents hairpin turns")]
    [Min(0.01f)]
    public float minTurnRadius = 1.5f;

    [Tooltip("± random range of arc entry angle from direct approach direction")]
    [Range(0f, 180f)]
    public float arcAngleVariance = 60f;

    [Tooltip("Seconds between path recalculations")]
    [Min(0.01f)]
    public float replanInterval = 2f;

    [Tooltip("Random ± seconds added to replan interval each cycle")]
    [Min(0f)]
    public float replanJitter = 0.4f;

    [Tooltip("Max arc generation retries before accepting the best attempt")]
    [Min(1)]
    public int maxReplanAttempts = 8;

    [Tooltip("Lateral peak displacement of the sinusoidal wriggle wave")]
    [Min(0f)]
    public float waveAmplitude = 0.4f;

    [Tooltip("Wave oscillations per second")]
    [Min(0f)]
    public float waveFrequency = 2f;

    [Tooltip("Phase shift per body node so the wave appears to travel tail-ward")]
    [Min(0f)]
    public float wavePhaseOffsetPerNode = 0.3f;

    [Tooltip("Seconds before another collision response can trigger")]
    [Min(0f)]
    public float collisionCooldownDuration = 0.5f;

    [Tooltip("Distance to player target that counts as arc arrival and triggers replan")]
    [Min(0f)]
    public float targetArrivalRadius = 0.5f;

    [Header("Prefab Overrides")]
    [Tooltip("Override head prefab (falls back to assembler default if null)")]
    public GameObject headPrefab;

    [Tooltip("Override body prefab (falls back to assembler default if null)")]
    public GameObject bodyPrefab;
}
