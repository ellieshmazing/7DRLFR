#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class CreateTuningDimensions
{
    const string FOLDER = "Assets/Prefabs/Dimension Definitions";

    [MenuItem("Tools/Tuning/Create Dimension Definitions")]
    static void CreateAll()
    {
        // ── Find configs ─────────────────────────────────────────────────────
        var playerConfig = FindAsset<PlayerConfig>("PlayerDefaultConfig")
                        ?? FindFirstAsset<PlayerConfig>();
        var centipedeConfig = FindFirstAsset<CentipedeConfig>();

        if (playerConfig == null)
            Debug.LogWarning("[TuningSetup] No PlayerConfig found — player dims will have null targetConfig.");
        else
            Debug.Log($"[TuningSetup] Using PlayerConfig: {playerConfig.name}");

        if (centipedeConfig == null)
            Debug.LogWarning("[TuningSetup] No CentipedeConfig found — centipede dims will have null targetConfig.");
        else
            Debug.Log($"[TuningSetup] Using CentipedeConfig: {centipedeConfig.name}");

        // ── 1  Foot Physics ──────────────────────────────────────────────────
        Create("01_FootPhysics", "Foot Physics",
            "Drop from height. Jump repeatedly. Watch landing arc and fall speed. Heavier = less jump height.",
            V(playerConfig, "footMass",         0.1f,  5f,   1f,   respawn: true),
            V(playerConfig, "footGravityScale", 0.3f,  3f,   1f,   respawn: true));

        // ── 2  Movement ──────────────────────────────────────────────────────
        Create("02_Movement", "Movement",
            "Run left-right. Start and stop. Try to dodge. moveForce = acceleration feel, maxSpeed = top speed feel.",
            V(playerConfig, "moveForce", 5f,  50f, 15f),
            V(playerConfig, "maxSpeed",  2f,  15f,  5f));

        // ── 3  Foot Spring ───────────────────────────────────────────────────
        Create("03_FootSpring", "Foot Spring",
            "Run and stop suddenly. Watch feet settle. Change direction. Low damping = swingy feet.",
            V(playerConfig, "footFrequency",    5f,  20f, 10.95f),
            V(playerConfig, "footDampingRatio", 0.3f, 1.2f, 0.73f));

        // ── 4  Hip Spring ────────────────────────────────────────────────────
        Create("04_HipSpring", "Hip Spring",
            "Jump and land. Watch torso bob. Higher frequency = faster recovery. Low damping = head-bobbing.",
            V(playerConfig, "hipFrequency",    5f,  25f, 10.95f),
            V(playerConfig, "hipDampingRatio", 0.2f, 1.2f, 0.46f));

        // ── 5  Torso Spring ──────────────────────────────────────────────────
        Create("05_TorsoSpring", "Torso Spring",
            "Change direction rapidly. Watch torso visual lag behind skeleton node. Pure visual polish.",
            V(playerConfig, "torsoFrequency",    4f,  20f,  8.94f, live: true),
            V(playerConfig, "torsoDampingRatio", 0.1f, 1.5f, 0.28f, live: true));

        // ── 6  Stance Geometry ───────────────────────────────────────────────
        Create("06_StanceGeometry", "Stance Geometry",
            "Stand still. Run. Jump. Evaluate silhouette proportions and stability feel.",
            V(playerConfig, "standHeight", 6f,  20f, 12f),
            V(playerConfig, "footSpreadX", 2f,  10f,  4f));

        // ── 7  Jump Feel ─────────────────────────────────────────────────────
        Create("07_JumpFeel", "Jump Feel",
            "Jump from flat ground (tests jumpSpeed). Crouch into ground then jump (tests offsetFactor). Cross a gap.",
            V(playerConfig, "jumpSpeed",        3f,  20f,  8f),
            V(playerConfig, "jumpOffsetFactor", 0f,  25f, 10f));

        // ── 8  Gun Feel ──────────────────────────────────────────────────────
        Create("08_GunFeel", "Gun Feel",
            "Shoot at targets at near, mid, and far range. Hold fire button for sustained fire.",
            V(playerConfig, "firingSpeed",   3f,    25f,  10f),
            V(playerConfig, "fireCooldown",  0.05f,  0.5f, 0.25f));

        // ── 9  Body Spring ───────────────────────────────────────────────────
        Create("09_BodySpring", "Body Spring",
            "Watch centipede traverse. Hit it with a projectile. Observe wobble and recovery.",
            V(centipedeConfig, "wiggleFrequency",    4f,  20f,  8.94f, live: true),
            V(centipedeConfig, "wiggleDampingRatio", 0.1f, 1.2f, 0.28f, live: true));

        // ── 10 Body Geometry ─────────────────────────────────────────────────
        Create("10_BodyGeometry", "Body Geometry",
            "Watch centipede at rest and in motion. Evaluate spacing and body proportions.",
            V(centipedeConfig, "followDistance", 0.1f,  0.8f, 0.3f,  respawn: true),
            V(centipedeConfig, "nodeRadius",     0.05f, 0.4f, 0.15f, respawn: true));

        // ── 11 Destruction ───────────────────────────────────────────────────
        Create("11_Destruction", "Destruction",
            "Shoot centipede with different projectile sizes. How hard is it to break? Too easy = trivial. Too hard = frustrating.",
            V(centipedeConfig, "detachDistance", 0.2f, 1.5f, 0.5f));

        // ── 12 Pathing Speed ─────────────────────────────────────────────────
        Create("12_PathingSpeed", "Pathing Speed",
            "Let centipede chase. Feel the threat level. High speed + low turn radius = aggressive.",
            V(centipedeConfig, "speed",         1f,   8f,  3f,   respawn: true),
            V(centipedeConfig, "minTurnRadius", 0.5f, 4f,  1.5f, respawn: true));

        // ── 13 Pathing Behavior ──────────────────────────────────────────────
        Create("13_PathingBehavior", "Pathing Behavior",
            "Watch approach patterns for 30+ seconds. High variance = unpredictable. Low replan = adaptive.",
            V(centipedeConfig, "arcAngleVariance", 10f,  120f, 60f, respawn: true),
            V(centipedeConfig, "replanInterval",   0.5f,   4f,  2f, respawn: true));

        // ── 14 Wriggle Feel ──────────────────────────────────────────────────
        Create("14_WriggleFeel", "Wriggle Feel",
            "Watch centipede approach from a distance. Evaluate character and organic feel.",
            V(centipedeConfig, "waveAmplitude",          0.1f, 1.0f, 0.4f,  respawn: true),
            V(centipedeConfig, "waveFrequency",          0.5f, 5f,   2f,    respawn: true),
            V(centipedeConfig, "wavePhaseOffsetPerNode", 0.1f, 1.0f, 0.3f,  respawn: true));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TuningSetup] Done — 14 TuningDimensionDef assets written to " + FOLDER);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void Create(string filename, string name, string scenario, params TuningVariable[] vars)
    {
        string path = $"{FOLDER}/{filename}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TuningDimensionDef>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);

        var def = ScriptableObject.CreateInstance<TuningDimensionDef>();
        def.dimensionName = name;
        def.testScenario  = scenario;
        def.variables     = vars;
        def.sweepDuration = 10f;

        AssetDatabase.CreateAsset(def, path);
    }

    static TuningVariable V(ScriptableObject config, string field,
                             float min, float max, float def,
                             bool respawn = false, bool live = false)
    {
        return new TuningVariable
        {
            targetConfig    = config,
            fieldName       = field,
            min             = min,
            max             = max,
            defaultValue    = def,
            requiresRespawn = respawn,
            liveSync        = live,
        };
    }

    static T FindAsset<T>(string assetName) where T : ScriptableObject
    {
        foreach (var guid in AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == assetName) return asset;
        }
        return null;
    }

    static T FindFirstAsset<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length > 1)
            Debug.LogWarning($"[TuningSetup] Multiple {typeof(T).Name} assets found — using first. " +
                             "Verify targetConfig references in the dimension defs.");
        foreach (var guid in guids)
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        return null;
    }
}
#endif
