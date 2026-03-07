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

        // ── Player Dimensions ─────────────────────────────────────────────────

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

        // ── 3  Walk Shape ────────────────────────────────────────────────────
        Create("03_WalkShape", "Walk Shape",
            "Run, reverse, stop abruptly. Watch when feet lift and where they plant. High trigger = lazy wide gait; low = hyperactive. High height = floaty arc; low = shuffle.",
            V(playerConfig, "strideTriggerDistance", 2f,    12f,  5f),
            V(playerConfig, "strideProjectionTime",  0.05f,  0.3f, 0.15f),
            V(playerConfig, "stepHeight",            1f,    12f,  4f));

        // ── 4  Walk Timing ───────────────────────────────────────────────────
        Create("04_WalkTiming", "Walk Timing",
            "Walk at slow, medium, and fast speeds. High base = plodding. High speedScale = steps collapse at speed. IdleThreshold = where return-to-neutral triggers.",
            V(playerConfig, "baseStepDuration",    0.1f,  0.4f,  0.2f),
            V(playerConfig, "minStepDuration",     0.02f, 0.15f, 0.06f),
            V(playerConfig, "stepSpeedScale",      0.05f, 0.6f,  0.3f),
            V(playerConfig, "idleSpeedThreshold",  0.1f,  1.5f,  0.5f));

        // ── 5  Foot Spring ───────────────────────────────────────────────────
        Create("05_FootSpring", "Foot Spring",
            "Jump and let feet hang airborne. Watch them spring toward formation. Low damping = swingy; high = snaps rigid.",
            V(playerConfig, "footFrequency",    5f,  20f, 10.95f),
            V(playerConfig, "footDampingRatio", 0.3f, 1.2f, 0.73f));

        // ── 6  Hip Spring ────────────────────────────────────────────────────
        Create("06_HipSpring", "Hip Spring",
            "Jump and land. Watch torso bob. Higher frequency = faster recovery. Low damping = head-bobbing.",
            V(playerConfig, "hipFrequency",    5f,  25f, 10.95f),
            V(playerConfig, "hipDampingRatio", 0.2f, 1.2f, 0.46f));

        // ── 7  Torso Spring ──────────────────────────────────────────────────
        Create("07_TorsoSpring", "Torso Spring",
            "Change direction rapidly. Watch torso visual lag behind skeleton node. Pure visual polish.",
            V(playerConfig, "torsoFrequency",    4f,  20f,  8.94f, live: true),
            V(playerConfig, "torsoDampingRatio", 0.1f, 1.5f, 0.28f, live: true));

        // ── 8  Stance Geometry ───────────────────────────────────────────────
        Create("08_StanceGeometry", "Stance Geometry",
            "Stand still. Run. Jump. Evaluate silhouette proportions and stability feel.",
            V(playerConfig, "standHeight", 6f,  20f, 12f),
            V(playerConfig, "footSpreadX", 2f,  10f,  4f));

        // ── 9  Jump Feel ─────────────────────────────────────────────────────
        Create("09_JumpFeel", "Jump Feel",
            "Jump from flat ground (tests jumpSpeed). Crouch into ground then jump (tests offsetFactor). Cross a gap.",
            V(playerConfig, "jumpSpeed",        3f,  20f,  8f),
            V(playerConfig, "jumpOffsetFactor", 0f,  25f, 10f));

        // ── 10 Gun Feel ──────────────────────────────────────────────────────
        Create("10_GunFeel", "Gun Feel",
            "Shoot at targets at near, mid, and far range. Hold fire button for sustained fire.",
            V(playerConfig, "firingSpeed",   3f,    25f,  10f),
            V(playerConfig, "fireCooldown",  0.05f,  0.5f, 0.25f));

        // ── Centipede Dimensions ──────────────────────────────────────────────

        // ── 11 Body Spring ───────────────────────────────────────────────────
        Create("11_BodySpring", "Body Spring",
            "Watch centipede traverse. Hit it with a projectile. Observe wobble and recovery.",
            V(centipedeConfig, "wiggleFrequency",    4f,  20f,  8.94f, live: true),
            V(centipedeConfig, "wiggleDampingRatio", 0.1f, 1.2f, 0.28f, live: true));

        // ── 12 Body Geometry ─────────────────────────────────────────────────
        Create("12_BodyGeometry", "Body Geometry",
            "Watch centipede at rest and in motion. Evaluate spacing and body proportions.",
            V(centipedeConfig, "followDistance", 0.1f,  0.8f, 0.3f,  respawn: true),
            V(centipedeConfig, "nodeRadius",     0.05f, 0.4f, 0.15f, respawn: true));

        // ── 13 Destruction ───────────────────────────────────────────────────
        Create("13_Destruction", "Destruction",
            "Shoot centipede with different projectile sizes. How hard is it to break? Too easy = trivial. Too hard = frustrating.",
            V(centipedeConfig, "detachDistance", 0.2f, 1.5f, 0.5f));

        // ── 14 Pathing Speed (arc only — skipped when useScentNavigator) ─────
        Create("14_PathingSpeed", "Pathing Speed",
            "[Arc navigator] Let centipede chase. Feel the threat level. High speed + low turn radius = aggressive.",
            V(centipedeConfig, "speed",         1f,   8f,  3f,   respawn: true),
            V(centipedeConfig, "minTurnRadius", 0.5f, 4f,  1.5f, respawn: true));

        // ── 15 Pathing Behavior (arc only — skipped when useScentNavigator) ──
        Create("15_PathingBehavior", "Pathing Behavior",
            "[Arc navigator] Watch approach patterns for 30+ seconds. High variance = unpredictable. Low replan = adaptive.",
            V(centipedeConfig, "arcAngleVariance", 10f,  120f, 60f, respawn: true),
            V(centipedeConfig, "replanInterval",   0.5f,   4f,  2f, respawn: true));

        // ── 16 Wriggle Feel ──────────────────────────────────────────────────
        Create("16_WriggleFeel", "Wriggle Feel",
            "Watch centipede approach from a distance. Evaluate character and organic feel.",
            V(centipedeConfig, "waveAmplitude",          0.1f, 1.0f, 0.4f,  respawn: true),
            V(centipedeConfig, "waveFrequency",          0.5f, 5f,   2f,    respawn: true),
            V(centipedeConfig, "wavePhaseOffsetPerNode", 0.1f, 1.0f, 0.3f,  respawn: true));

        // ── Scent Navigator Dimensions (used instead of 14–15 when useScentNavigator == true) ──

        // ── 17 Scent Trail ───────────────────────────────────────────────────
        Create("17_ScentTrail", "Scent Trail",
            "[Scent] Stand still 10s, move away, stand still again. Watch centipede follow the ghost path. High decayTime = long memory. High sigma = blurry wide trail, gradient activates earlier.",
            V(centipedeConfig, "scentDecayTime", 3f,   20f, 8f),
            V(centipedeConfig, "scentSigma",     0.5f,  3f, 1.5f));

        // ── 18 Scent Consumption ─────────────────────────────────────────────
        Create("18_ScentConsumption", "Scent Consumption",
            "[Scent] Stand completely still. Watch centipede spiral in. High rate + wide radius = tight decisive spiral. Low = looser circles, may drift past and arc back.",
            V(centipedeConfig, "scentConsumeRate",   0.5f, 5f,   2f),
            V(centipedeConfig, "scentConsumeRadius", 0.3f, 1.5f, 0.6f));

        // ── 19 Hunting Rhythm ────────────────────────────────────────────────
        Create("19_HuntingRhythm", "Hunting Rhythm",
            "[Scent] Run gentle curves for 20+ seconds. Watch heading changes. Low blend + low oscillation = slow deliberate sweep. High blend + high oscillation = jittery zigzag. High sample radius = smooth global steering.",
            V(centipedeConfig, "scentSteeringBlend",         1f,  10f,  4f),
            V(centipedeConfig, "scentOscillationFrequency",  0.1f, 1f,  0.35f),
            V(centipedeConfig, "scentGradientSampleRadius",  0.3f, 2f,  0.8f));

        // ── 20 Trail Speed ───────────────────────────────────────────────────
        Create("20_TrailSpeed", "Trail Speed",
            "[Scent] Sprint in a straight line, then stop abruptly. Watch if centipede surges. Calibrate scentGradientMaxStrength first (sweep until boost is clearly visible), then tune scentSpeedBoost for threat level.",
            V(centipedeConfig, "scentSpeedBoost",          0f,  3f,  1f),
            V(centipedeConfig, "scentGradientMaxStrength", 1f, 10f,  5f));

        // ── 21 Fallback Behavior ─────────────────────────────────────────────
        Create("21_FallbackBehavior", "Fallback Behavior",
            "[Scent] Hide behind cover for 30+ seconds until field fully decays. High threshold = centipede switches to direct chase quickly. High blend = fast snap-to-player once fallback activates.",
            V(centipedeConfig, "scentFallbackThreshold", 0.01f, 0.2f, 0.05f),
            V(centipedeConfig, "scentFallbackBlend",     0.2f,  2f,   0.8f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TuningSetup] Done — 21 TuningDimensionDef assets written to " + FOLDER);
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
