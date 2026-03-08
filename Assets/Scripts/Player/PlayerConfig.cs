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

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // PLAYER DIMENSIONS (TUNE FIRST)
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Header("1. Foot Physics")]
    [Tooltip("Range: 0.1–5. Heavier = less jump height.")]
    [Min(0.01f)] public float footMass = 1f;
    [Tooltip("Range: 0.3–3. Increase for snappier landings; decrease for floatier jumps.")]
    [Min(0f)] public float footGravityScale = 1f;
    // TEST: Drop from height. Jump repeatedly. Watch landing arc and fall speed.

    [Header("2. Movement")]
    [Tooltip("Range: 5–50. Acceleration feel.")]
    [Min(0.1f)] public float moveForce = 15f;
    [Tooltip("Range: 2–15. Top speed feel.")]
    [Min(0.1f)] public float maxSpeed = 5f;
    // TEST: Run left-right. Start and stop. Try to dodge.

    [Header("3. Walking Trigger")]
    [Tooltip("Range: 1–12 px. Low = twitchy constant stepping. High = shuffling lag.")]
    [Min(0f)] public float strideTriggerDistance = 5f;
    [Tooltip("Range: 0.1–2.0. Speed below which player is considered idle and feet correct to neutral stance.")]
    [Min(0f)] public float idleSpeedThreshold = 0.5f;
    [Tooltip("Range: 0.02–0.4 s. How far ahead feet reach on fast strides.")]
    [Min(0f)] public float strideProjectionTime = 0.15f;
    // TEST: Run at various speeds. Watch when feet step.

    [Header("4. Step Shape")]
    [Tooltip("Range: 1–12 px. High stepHeight = marching lift; low = shuffle.")]
    [Min(0f)] public float stepHeight = 4f;
    [Tooltip("Range: 0.05–0.5 s. Time for one step at zero horizontal speed.")]
    [Min(0.01f)] public float baseStepDuration = 0.2f;
    [Tooltip("Range: 0.02–0.15 s. Minimum step duration at high speeds — prevents infinitely fast leg cycling.")]
    [Min(0.001f)] public float minStepDuration = 0.06f;
    [Tooltip("Range: 0.05–1.0. How much a sprint compresses step duration: duration = base / (1 + speed * scale).")]
    [Min(0f)] public float stepSpeedScale = 0.3f;
    // TEST: Walk slowly, then sprint. Watch arc height and cadence.

    [Header("5. Foot Spring")]
    [Tooltip("Range: 5–20 rad/s. Higher = feet snap to formation faster.")]
    [Min(0.01f)] public float footFrequency = 10.95f;
    [Tooltip("Range: 0.3–1.2. Lower = feet oscillate past formation; high = feet snap immediately.")]
    [Min(0f)] public float footDampingRatio = 0.73f;
    [Tooltip("Spring simulation mass; independent of footMass (RB mass).")]
    [Min(0.01f)] public float footSpringMass = 0.5f;
    public float FootStiffness => SpringParams.ComputeStiffness(footFrequency, footSpringMass);
    public float FootDamping   => SpringParams.ComputeDamping(footFrequency, footDampingRatio, footSpringMass);
    // TEST: Jump and land; watch feet during airborne phase and on touchdown.

    [Header("6. Hip Spring")]
    [Tooltip("Range: 5–25 rad/s. Higher frequency = faster recovery.")]
    [Min(0.01f)] public float hipFrequency = 10.95f;
    [Tooltip("Range: 0.2–1.2. Low damping = head-bobbing.")]
    [Min(0f)] public float hipDampingRatio = 0.46f;
    [Tooltip("Hip spring mass; also divides jump impulse.")]
    [Min(0.01f)] public float hipMass = 1f;
    public float HipStiffness => SpringParams.ComputeStiffness(hipFrequency, hipMass);
    public float HipDamping   => SpringParams.ComputeDamping(hipFrequency, hipDampingRatio, hipMass);
    // TEST: Jump and land. Watch torso bob.

    [Header("7. Torso Spring")]
    [Tooltip("Range: 4–20 rad/s. Higher = snappier visual tracking.")]
    [Min(0.01f)] public float torsoFrequency = 8.94f;
    [Tooltip("Range: 0.1–1.5. 0 = perpetual bounce, 1 = critically damped.")]
    [Min(0f)] public float torsoDampingRatio = 0.28f;
    [Tooltip("Spring simulation mass; affects response to external forces without changing spring feel.")]
    [Min(0.01f)] public float torsoMass = 1f;
    public float TorsoStiffness => SpringParams.ComputeStiffness(torsoFrequency, torsoMass);
    public float TorsoDamping   => SpringParams.ComputeDamping(torsoFrequency, torsoDampingRatio, torsoMass);
    // TEST: Change direction rapidly. Watch torso visual lag behind skeleton node. Pure visual polish.

    [Header("8. Stance Geometry")]
    [Tooltip("Range: 6–20 px. Distance from torso node to lowest foot visual.")]
    public float standHeight = 12f;
    [Tooltip("Range: 2–10 px. Horizontal distance from torso to each foot node.")]
    [Min(0f)] public float footSpreadX = 4f;
    [Tooltip("Vertical offset of foot nodes below torso (negative = below), in source pixels.")]
    public float footOffsetY = -8f;
    // TEST: Stand still. Run. Jump. Evaluate silhouette proportions and stability feel.

    [Header("9. Jump Feel")]
    [Tooltip("Range: 3–20. Base jump impulse (kg·m/s); actual velocity = jumpSpeed / footMass.")]
    [Min(0f)] public float jumpSpeed = 8f;
    [Tooltip("Range: 0–25. Extra impulse per world-unit of hip compression.")]
    [Min(0f)] public float jumpOffsetFactor = 10f;
    // TEST: Jump from flat ground (tests jumpSpeed). Crouch into ground then jump (tests offsetFactor). Cross a gap.

    [Header("10. Gun Feel")]
    [Tooltip("Range: 3–25 wu/s. World units per second of fired projectiles.")]
    [Min(0.1f)] public float firingSpeed = 10f;
    [Tooltip("Range: 0.05–0.5 s. Minimum seconds between shots.")]
    [Min(0f)] public float fireCooldown = 0.25f;
    // TEST: Shoot at targets at near, mid, and far range. Hold fire button for sustained fire.

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // VISUALS
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Header("Torso Visual — Layers")]
    [Tooltip("One entry per sprite layer; all layers move as a single rigid group")]
    public TorsoLayerDef[] torsoLayers = new TorsoLayerDef[]
    {
        new TorsoLayerDef { color = Color.white, localOffset = Vector2.zero, sortingOrder = 0 }
    };

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

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // GUN & PROJECTILES
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Header("Gun")]
    [Tooltip("BallDefinition spawned on each shot")]
    public BallDefinition projectileDef;

    [Tooltip("Distance from the arm pivot to the barrel tip, in source pixels")]
    [Min(0f)] public float firingPointOffset = 8f;

    [Tooltip("Starting world-unit diameter of a projectile at spawn — creates barrel-emergence illusion")]
    [Min(0.001f)] public float projectileInitialScale = 0.05f;

    [Tooltip("Seconds for a projectile to grow from spawn scale to its true diameter")]
    [Min(0.001f)] public float projectileGrowTime = 0.15f;

    [Tooltip("[TEMP] Minimum random projectile diameter in world units")]
    [Min(0.01f)] public float tempMinProjectileDiameter = 0.2f;

    [Tooltip("[TEMP] Maximum random projectile diameter in world units")]
    [Min(0.01f)] public float tempMaxProjectileDiameter = 0.8f;

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // FOOT MOVEMENT — SECONDARY PARAMETERS
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Header("Foot Movement Setup")]
    [Tooltip("Max angle (degrees) between surface normal and Vector2.up for a surface to count as walkable. " +
             "0 = flat ground only, 90 = any surface. try 30–60")]
    [Min(0f)] public float maxWalkableAngle = 50f;

    [Tooltip("Raycast distance when probing for step target ground, in source pixels. Must exceed torso-to-ground distance. try 20–50")]
    [Min(1f)] public float stepRaycastDistance = 30f;

    [Header("Airborne Foot X Tracking")]
    [Tooltip("X displacement (source pixels) below which an airborne foot's X snaps and locks to its target. try 0.5–2")]
    [Min(0f)] public float footXLockThreshold = 1f;
    [Tooltip("X velocity (wu/s) below which an airborne foot's X can snap-lock. try 0.05–0.3")]
    [Min(0f)] public float footXLockVelocity = 0.1f;
    [Tooltip("X displacement (source pixels) above which a locked airborne foot's X releases back to spring. try 2–5")]
    [Min(0f)] public float footXUnlockThreshold = 3f;

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // DEBUG
    // ════════════════════════════════════════════════════════════════════════════════════════════

    [Header("Debug Gizmos")]
    public Color torsoNodeGizmoColor = Color.cyan;
    public Color footNodeGizmoColor  = Color.yellow;
}
