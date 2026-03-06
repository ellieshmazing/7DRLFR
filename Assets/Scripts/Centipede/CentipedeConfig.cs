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

    [Header("Prefab Overrides")]
    [Tooltip("Override head prefab (falls back to assembler default if null)")]
    public GameObject headPrefab;

    [Tooltip("Override body prefab (falls back to assembler default if null)")]
    public GameObject bodyPrefab;
}
