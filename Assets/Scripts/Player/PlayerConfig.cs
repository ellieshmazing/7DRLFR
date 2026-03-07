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

    [Header("Spring — Torso")]
    [Tooltip("Natural frequency ω (rad/s); higher = snappier visual tracking")]
    [Min(0.01f)] public float torsoFrequency = 8.94f;
    [Tooltip("Damping ratio ζ; 0 = perpetual bounce, 1 = critically damped")]
    [Min(0f)] public float torsoDampingRatio = 0.28f;
    [Tooltip("Spring simulation mass; affects response to external forces without changing spring feel")]
    [Min(0.01f)] public float torsoMass = 1f;

    public float TorsoStiffness => SpringParams.ComputeStiffness(torsoFrequency, torsoMass);
    public float TorsoDamping   => SpringParams.ComputeDamping(torsoFrequency, torsoDampingRatio, torsoMass);

    [Header("Spring — Hip")]
    [Tooltip("Natural frequency ω (rad/s); higher = torso snaps to foot level faster")]
    [Min(0.01f)] public float hipFrequency = 10.95f;
    [Tooltip("Damping ratio ζ; lower = more torso bob on landing")]
    [Min(0f)] public float hipDampingRatio = 0.46f;
    [Tooltip("Hip spring mass; also divides jump impulse")]
    [Min(0.01f)] public float hipMass = 1f;

    public float HipStiffness => SpringParams.ComputeStiffness(hipFrequency, hipMass);
    public float HipDamping   => SpringParams.ComputeDamping(hipFrequency, hipDampingRatio, hipMass);

    [Header("Spring — Feet")]
    [Tooltip("Natural frequency ω (rad/s); higher = feet snap to formation faster")]
    [Min(0.01f)] public float footFrequency = 10.95f;
    [Tooltip("Damping ratio ζ; lower = feet wobble after direction changes")]
    [Min(0f)] public float footDampingRatio = 0.73f;
    [Tooltip("Foot spring simulation mass; independent of footMass (RB mass)")]
    [Min(0.01f)] public float footSpringMass = 0.5f;

    public float FootStiffness => SpringParams.ComputeStiffness(footFrequency, footSpringMass);
    public float FootDamping   => SpringParams.ComputeDamping(footFrequency, footDampingRatio, footSpringMass);

    [Header("Jump")]
    [Tooltip("Base jump impulse (kg·m/s); actual velocity = jumpSpeed / footMass")]
    [Min(0f)] public float jumpSpeed = 8f;
    [Tooltip("Extra impulse per world-unit of hip compression")]
    [Min(0f)] public float jumpOffsetFactor = 10f;

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

    [Header("Gun")]
    [Tooltip("BallDefinition spawned on each shot")]
    public BallDefinition projectileDef;

    [Tooltip("Distance from the arm pivot to the barrel tip, in source pixels")]
    [Min(0f)] public float firingPointOffset = 8f;

    [Tooltip("World units per second of fired projectiles")]
    [Min(0.1f)] public float firingSpeed = 10f;

    [Tooltip("Minimum seconds between shots")]
    [Min(0f)] public float fireCooldown = 0.25f;

    [Tooltip("Starting world-unit diameter of a projectile at spawn — creates barrel-emergence illusion")]
    [Min(0.001f)] public float projectileInitialScale = 0.05f;

    [Tooltip("Seconds for a projectile to grow from spawn scale to its true diameter")]
    [Min(0.001f)] public float projectileGrowTime = 0.15f;

    [Tooltip("[TEMP] Minimum random projectile diameter in world units")]
    [Min(0.01f)] public float tempMinProjectileDiameter = 0.2f;

    [Tooltip("[TEMP] Maximum random projectile diameter in world units")]
    [Min(0.01f)] public float tempMaxProjectileDiameter = 0.8f;

    [Header("Feet")]
    public FootDef leftFoot  = new FootDef { color = Color.white, sortingOrder = -1 };
    public FootDef rightFoot = new FootDef { color = Color.white, sortingOrder = -1 };

    [Header("Debug Gizmos")]
    public Color torsoNodeGizmoColor = Color.cyan;
    public Color footNodeGizmoColor  = Color.yellow;
}
