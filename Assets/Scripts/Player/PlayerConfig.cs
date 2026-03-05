using UnityEngine;

[System.Serializable]
public struct TorsoLayerDef
{
    public Sprite  sprite;
    public Color   color;
    [Tooltip("Offset from TorsoVisual centre, in source pixels")]
    public Vector2 localOffset;
    public int     sortingOrder;
}

[System.Serializable]
public struct ArmLayerDef
{
    public Sprite  sprite;
    public Color   color;
    [Tooltip("Offset from Arm pivot, in source pixels")]
    public Vector2 localOffset;
    public int     sortingOrder;
}

[System.Serializable]
public struct FootDef
{
    public Sprite sprite;
    public Color  color;
    public int    sortingOrder;
}

[CreateAssetMenu(fileName = "NewPlayerConfig", menuName = "Player/Config")]
public class PlayerConfig : ScriptableObject
{
    [Header("Scale")]
    [Tooltip("World-unit size of one 16-pixel sprite. " +
             "All pixel-space offsets and distances scale with this.")]
    [Min(0.01f)] public float playerScale = 0.5f;

    [Header("Movement")]
    [Min(0.1f)] public float moveForce = 15f;
    [Min(0.1f)] public float maxSpeed = 5f;

    [Header("Stance")]
    [Tooltip("Distance from torso node to lowest foot visual, in source pixels")]
    public float standHeight = 12f;

    [Header("Foot Formation")]
    [Tooltip("Horizontal distance from torso to each foot node, in source pixels")]
    [Min(0f)] public float footSpreadX = 4f;
    [Tooltip("Vertical offset of foot nodes below torso (negative = below), in source pixels")]
    public float footOffsetY = -8f;

    [Header("Torso Visual — Layers")]
    [Tooltip("One entry per sprite layer; all layers move as a single rigid group")]
    public TorsoLayerDef[] torsoLayers = new TorsoLayerDef[]
    {
        new TorsoLayerDef { color = Color.white, localOffset = Vector2.zero, sortingOrder = 0 }
    };

    [Header("Wiggle — Torso")]
    [Min(0f)] public float torsoWiggleStiffness = 80f;
    [Min(0f)] public float torsoWiggleDamping = 5f;
    [Min(0.01f)] public float torsoWiggleMass = 1f;

    [Header("Wiggle — Hip")]
    [Tooltip("Spring pulling the hip node toward the lowest foot Y")]
    [Min(0f)] public float hipWiggleStiffness = 120f;
    [Min(0f)] public float hipWiggleDamping = 10f;
    [Min(0.01f)] public float hipWiggleMass = 1f;

    [Header("Wiggle — Feet")]
    [Tooltip("Spring pulling each foot toward its target X and toward hip Y")]
    [Min(0f)] public float footWiggleStiffness = 60f;
    [Min(0f)] public float footWiggleDamping = 8f;
    [Min(0.01f)] public float footWiggleMass = 0.5f;

    [Header("Foot Physics")]
    [Tooltip("Mass of each foot Rigidbody2D. Affects gravity response, collision impulses, " +
             "and jump height — heavier feet require more jump impulse for the same height.")]
    [Min(0.01f)] public float footMass = 1f;

    [Tooltip("Gravity scale applied to each foot Rigidbody2D. " +
             "Increase for snappier landings; decrease for floatier jumps.")]
    [Min(0f)] public float footGravityScale = 1f;

    [Header("Arms")]
    [Tooltip("Orbit distance from TorsoVisual centre to Arm pivot, in source pixels")]
    [Min(0f)] public float armOrbitRadius = 10f;
    [Tooltip("One entry per arm sprite layer; all layers are children of the same Arm pivot")]
    public ArmLayerDef[] armLayers = new ArmLayerDef[]
    {
        new ArmLayerDef { color = Color.white, localOffset = Vector2.zero, sortingOrder = 1 }
    };

    [Header("Feet")]
    public FootDef leftFoot  = new FootDef { color = Color.white, sortingOrder = -1 };
    public FootDef rightFoot = new FootDef { color = Color.white, sortingOrder = -1 };

    [Header("Debug Gizmos")]
    public Color torsoNodeGizmoColor = Color.cyan;
    public Color footNodeGizmoColor  = Color.yellow;
}
