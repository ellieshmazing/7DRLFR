using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Singleton MonoBehaviour that orchestrates the runtime tuning system.
///
/// Phases:
///   Idle            — tuning inactive, game plays normally.
///   Sweep           — sinusoidal sweep of the current variable from min to max.
///                     User logs interesting values, then locks to enter AB.
///   AB              — ternary-search bracket that converges on a preferred value.
///   CrossValidation — perturbed full-config variants compared against the base.
///   Complete        — all dimensions tuned.
///
/// Keyboard driven (New Input System only). All config values are written to SOs
/// via reflection; live MonoBehaviour instances are optionally dual-written so
/// changes take effect without respawn.
/// </summary>
public class TuningManager : MonoBehaviour
{
    // ── Types ────────────────────────────────────────────────────────────────

    public enum TuningPhase { Idle, Sweep, AB, CrossValidation, Complete }

    enum EntityType { None, Player, Centipede }

    // ── Serialized fields ────────────────────────────────────────────────────

    [Header("Config References")]
    [SerializeField] PlayerConfig playerConfig;
    [SerializeField] CentipedeConfig centipedeConfig;
    [SerializeField] PlayerAssembler playerAssembler;
    [SerializeField] CentipedeAssembler centipedeAssembler;
    [SerializeField] SmoothFollowCamera followCamera;

    [Header("Dimensions")]
    [SerializeField] TuningDimensionDef[] dimensionDefs;

    [Header("Controls")]
    [SerializeField] Key tuningToggleKey = Key.Backquote;
    [SerializeField] Key nextDimensionKey = Key.Tab;
    [SerializeField] Key logKey = Key.F5;
    [SerializeField] Key lockKey = Key.Enter;
    [SerializeField] Key chooseAKey = Key.Digit1;
    [SerializeField] Key chooseBKey = Key.Digit2;
    [SerializeField] Key saveProfileKey = Key.F9;
    [SerializeField] Key loadProfileKey = Key.F10;
    [SerializeField] Key resetKey = Key.Backspace;
    [SerializeField] Key skipDimensionKey = Key.RightShift;

    [Header("Tuning Parameters")]
    [SerializeField] float sweepSpeedMultiplier = 1f;
    [SerializeField] float abSwapInterval = 3f;
    [Tooltip("Seconds the sweep pauses after each init-only respawn, letting the player observe the new value before continuing")]
    [SerializeField] float respawnObservationWindow = 2f;
    [SerializeField] float abEpsilon = 0.02f;
    [SerializeField] float crossValPerturbation = 0.10f;
    [SerializeField] int crossValVariants = 4;
    [SerializeField] float overlayWidth = 320f;
    [SerializeField] float overlayOpacity = 0.85f;

    [Header("Spawn Positions")]
    [SerializeField] Vector2 playerSpawnPosition = new Vector2(0f, 1f);
    [SerializeField] Vector2 centipedeSpawnPosition = new Vector2(0f, 3f);
    [SerializeField] int centipedeSpawnCount = 1;
    [SerializeField] float centipedeSpawnSpacing = 3f;

    [Header("AB Tones (Testing Only)")]
    [SerializeField] float abToneFreqA = 880f;
    [SerializeField] float abToneFreqB = 550f;
    [SerializeField] float abToneDuration = 0.12f;
    [SerializeField] float abToneVolume = 0.4f;

    // ── State ────────────────────────────────────────────────────────────────

    TuningPhase phase = TuningPhase.Idle;
    int dimensionIndex;
    int variableIndex;

    // Sweep
    float sweepElapsed;
    List<float> loggedValues = new List<float>();

    // Step-based sweep (requiresRespawn variables)
    float sweepStepCurrentValue;
    int sweepStepDirection = 1; // +1 ascending, -1 descending
    bool stepRespawnPending;    // true between ApplyValue and the respawn actually executing

    // AB
    float abLo, abHi, abA, abB;
    float abSwapTimer;
    bool showingA;
    int abRoundCount;

    // CrossValidation
    Dictionary<string, float> baseSnapshot;
    List<Dictionary<string, float>> crossValSnapshots;
    int crossValIndex;
    bool crossValShowingBase;

    // AB tones
    AudioSource abToneSource;
    AudioClip toneClipA;
    AudioClip toneClipB;

    // Respawn
    bool respawnPending;
    ScriptableObject respawnTargetConfig;
    float lastAppliedRespawnValue;
    Coroutine activeSpawnCoroutine;
    float postRespawnLockout; // sweep pause + respawn block after each init-only respawn

    // ── Public properties (for overlay) ──────────────────────────────────────

    public TuningPhase Phase => phase;

    public TuningDimensionDef CurrentDimension =>
        dimensionIndex < dimensionDefs.Length ? dimensionDefs[dimensionIndex] : null;

    public TuningVariable CurrentVariable =>
        CurrentDimension != null && variableIndex < CurrentDimension.variables.Length
            ? CurrentDimension.variables[variableIndex]
            : default;

    public int DimensionIndex => dimensionIndex;
    public int VariableIndex => variableIndex;
    public float CurrentValue { get; private set; }
    public float SweepNormalized { get; private set; }
    public bool ShowingA => showingA;
    public float ABSwapTimer => abSwapTimer;
    public float ABLo => abLo;
    public float ABHi => abHi;
    public int ABRoundCount => abRoundCount;
    public List<float> LoggedValues => loggedValues;
    public float SweepSpeedMultiplier => sweepSpeedMultiplier;

    // ── Singleton ────────────────────────────────────────────────────────────

    public static TuningManager Instance { get; private set; }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ValidateDimensions();

        abToneSource = gameObject.AddComponent<AudioSource>();
        abToneSource.playOnAwake = false;
        toneClipA = GenerateToneClip(abToneFreqA, abToneDuration);
        toneClipB = GenerateToneClip(abToneFreqB, abToneDuration);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Toggle tuning on/off (available in any phase)
        if (kb[tuningToggleKey].wasPressedThisFrame)
        {
            ToggleTuning();
            return;
        }

        if (phase == TuningPhase.Idle || phase == TuningPhase.Complete) return;

        // Speed multiplier adjustment: [ and ]
        if (kb[Key.LeftBracket].wasPressedThisFrame)
            sweepSpeedMultiplier = Mathf.Clamp(sweepSpeedMultiplier - 0.1f, 0.1f, 5f);
        if (kb[Key.RightBracket].wasPressedThisFrame)
            sweepSpeedMultiplier = Mathf.Clamp(sweepSpeedMultiplier + 0.1f, 0.1f, 5f);

        // Profile save/load
        if (kb[saveProfileKey].wasPressedThisFrame)
            SaveProfile();

        // Navigation: next dimension (Tab), prev dimension (Shift+Tab)
        bool shiftHeld = kb.leftShiftKey.isPressed;
        if (kb.tabKey.wasPressedThisFrame)
        {
            if (shiftHeld)
                PrevDimension();
            else
                NextDimension();
            return;
        }

        // Skip dimension without tuning
        if (kb[skipDimensionKey].wasPressedThisFrame)
        {
            SkipDimension();
            return;
        }

        // Reset current dimension to defaults
        if (kb[resetKey].wasPressedThisFrame)
        {
            ResetCurrentDimension();
            return;
        }

        // Phase-specific update
        switch (phase)
        {
            case TuningPhase.Sweep:
                UpdateSweep(kb);
                break;
            case TuningPhase.AB:
                UpdateAB(kb);
                break;
            case TuningPhase.CrossValidation:
                UpdateCrossValidation(kb);
                break;
        }
    }

    void LateUpdate()
    {
        if (!respawnPending) return;
        respawnPending = false;

        if (respawnTargetConfig is PlayerConfig pc)
        {
            // Defer respawn until a foot is grounded to avoid mid-air respawn glitches
            var player = FindAnyObjectByType<PlayerSkeletonRoot>();
            if (player != null)
            {
                bool grounded = (player.leftFootContact != null && player.leftFootContact.isGrounded)
                             || (player.rightFootContact != null && player.rightFootContact.isGrounded);
                if (!grounded)
                {
                    respawnPending = true;
                    return;
                }
            }
            stepRespawnPending = false;
            postRespawnLockout = respawnObservationWindow;
            StartCoroutine(AutoRespawner.RespawnPlayer(pc, playerAssembler));
        }
        else if (respawnTargetConfig is CentipedeConfig cc)
        {
            stepRespawnPending = false;
            postRespawnLockout = respawnObservationWindow;
            StartCoroutine(RespawnCentipedesAndUpdateCamera(cc, centipedeAssembler));
        }
    }

    // ── Sweep phase ──────────────────────────────────────────────────────────

    void UpdateSweep(Keyboard kb)
    {
        var v = CurrentVariable;
        if (CurrentDimension == null) return;

        // Init-only variables use a step-based ping-pong instead of the continuous sine.
        if (v.requiresRespawn)
        {
            UpdateSweepStep(kb, v);
            return;
        }

        // ── Continuous sine sweep (live variables) ────────────────────────────

        // During the post-respawn observation window the sweep is frozen but input
        // still works — the player can log the current value or lock early.
        if (postRespawnLockout > 0f)
            postRespawnLockout -= Time.deltaTime;
        else
            sweepElapsed += Time.deltaTime * sweepSpeedMultiplier;

        float normalized = (Mathf.Sin(sweepElapsed * 2f * Mathf.PI / CurrentDimension.sweepDuration
                                      - Mathf.PI / 2f) + 1f) / 2f;
        SweepNormalized = normalized;
        float value = Mathf.Lerp(v.min, v.max, normalized);

        if (kb[logKey].wasPressedThisFrame)
            loggedValues.Add(value);

        if (kb[lockKey].wasPressedThisFrame)
        {
            postRespawnLockout = 0f;
            TransitionToAB();
            return;
        }

        if (postRespawnLockout > 0f) return;

        ApplyValue(v, value);
    }

    /// <summary>
    /// Step-based sweep for requiresRespawn variables.
    /// After each respawn observation window, advances by respawnStepFraction of the range
    /// and bounces direction at the bounds — a linear ping-pong mirroring the sine reversal.
    /// </summary>
    void UpdateSweepStep(Keyboard kb, TuningVariable v)
    {
        // Tick down the observation lockout; don't advance while the player is watching.
        if (postRespawnLockout > 0f)
            postRespawnLockout -= Time.deltaTime;

        float range = v.max - v.min;
        SweepNormalized = range > 0f
            ? Mathf.Clamp01((sweepStepCurrentValue - v.min) / range)
            : 0f;

        if (kb[logKey].wasPressedThisFrame)
            loggedValues.Add(sweepStepCurrentValue);

        if (kb[lockKey].wasPressedThisFrame)
        {
            postRespawnLockout = 0f;
            TransitionToAB();
            return;
        }

        if (postRespawnLockout > 0f) return;

        // Block re-entry while waiting for a queued respawn to actually execute.
        // QueueRespawn only sets respawnPending; for players, LateUpdate defers
        // the actual spawn (and thus postRespawnLockout) until grounded. Without
        // this guard every airborne frame would advance the step again.
        if (stepRespawnPending) return;

        // First call after EnterSweep: apply min immediately (lastAppliedRespawnValue is NaN).
        // Subsequent calls: advance by one step before applying.
        if (!float.IsNaN(lastAppliedRespawnValue))
        {
            float stepSize = Mathf.Max(CurrentDimension.respawnStepFraction * range, 1e-6f);
            sweepStepCurrentValue += stepSize * sweepStepDirection;

            if (sweepStepDirection > 0 && sweepStepCurrentValue >= v.max)
            {
                sweepStepCurrentValue = v.max;
                sweepStepDirection = -1;
            }
            else if (sweepStepDirection < 0 && sweepStepCurrentValue <= v.min)
            {
                sweepStepCurrentValue = v.min;
                sweepStepDirection = 1;
            }
        }

        lastAppliedRespawnValue = sweepStepCurrentValue;
        stepRespawnPending = true;
        ApplyValue(v, sweepStepCurrentValue);
    }

    void TransitionToAB()
    {
        var v = CurrentVariable;
        float bracketMin, bracketMax;

        if (loggedValues.Count >= 2)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (float val in loggedValues)
            {
                if (val < lo) lo = val;
                if (val > hi) hi = val;
            }
            float spread = hi - lo;
            float padding = spread * 0.15f;
            bracketMin = Mathf.Clamp(lo - padding, v.min, v.max);
            bracketMax = Mathf.Clamp(hi + padding, v.min, v.max);
        }
        else
        {
            bracketMin = v.min;
            bracketMax = v.max;
        }

        EnterAB(bracketMin, bracketMax);
    }

    // ── AB phase ─────────────────────────────────────────────────────────────

    void UpdateAB(Keyboard kb)
    {
        abSwapTimer += Time.deltaTime;
        if (abSwapTimer >= abSwapInterval)
        {
            abSwapTimer = 0f;
            showingA = !showingA;
            PlayABTone(showingA);
            ApplyValue(CurrentVariable, showingA ? abA : abB);
        }

        if (kb[chooseAKey].wasPressedThisFrame)
        {
            // User prefers A — discard upper third (B side)
            abHi = abB;
            CommitChoice();
        }
        else if (kb[chooseBKey].wasPressedThisFrame)
        {
            // User prefers B — discard lower third (A side)
            abLo = abA;
            CommitChoice();
        }
    }

    void CommitChoice()
    {
        abRoundCount++;
        var v = CurrentVariable;
        float epsilon = (v.max - v.min) * abEpsilon;

        if ((abHi - abLo) < epsilon)
        {
            float final = (abHi + abLo) / 2f;
            ApplyValue(v, final);
            AdvanceToNextVariable();
        }
        else
        {
            abA = abLo + (abHi - abLo) / 3f;
            abB = abLo + 2f * (abHi - abLo) / 3f;
            abSwapTimer = 0f;
            showingA = true;
            PlayABTone(true);
            ApplyValue(v, abA);
        }
    }

    void AdvanceToNextVariable()
    {
        variableIndex++;
        bool newDimension = false;
        if (CurrentDimension == null || variableIndex >= CurrentDimension.variables.Length)
        {
            variableIndex = 0;
            dimensionIndex++;
            newDimension = true;
            if (dimensionIndex >= dimensionDefs.Length)
            {
                phase = TuningPhase.CrossValidation;
                EnterCrossValidation();
                return;
            }
        }
        if (newDimension)
            TriggerDimensionSpawn();
        EnterSweep();
    }

    // ── Cross-validation phase ───────────────────────────────────────────────

    void EnterCrossValidation()
    {
        // Snapshot the base (tuned) values
        baseSnapshot = TakeSnapshot();

        // Generate perturbed variants
        crossValSnapshots = new List<Dictionary<string, float>>();
        for (int i = 0; i < crossValVariants; i++)
        {
            var variant = new Dictionary<string, float>(baseSnapshot);
            foreach (var key in new List<string>(variant.Keys))
            {
                float baseVal = variant[key];
                float perturbAmount = baseVal * crossValPerturbation;
                float perturbed = baseVal + Random.Range(-perturbAmount, perturbAmount);
                variant[key] = perturbed;
            }
            crossValSnapshots.Add(variant);
        }

        crossValIndex = 0;
        crossValShowingBase = true;
        abSwapTimer = 0f;
        showingA = true; // true = showing base

        if (crossValSnapshots.Count == 0)
        {
            phase = TuningPhase.Complete;
            Debug.Log("[TuningManager] Tuning complete — no variants to cross-validate.");
            return;
        }

        ApplySnapshot(baseSnapshot);
    }

    void UpdateCrossValidation(Keyboard kb)
    {
        if (crossValSnapshots == null || crossValIndex >= crossValSnapshots.Count)
        {
            phase = TuningPhase.Complete;
            Debug.Log("[TuningManager] Tuning complete.");
            return;
        }

        abSwapTimer += Time.deltaTime;
        if (abSwapTimer >= abSwapInterval)
        {
            abSwapTimer = 0f;
            crossValShowingBase = !crossValShowingBase;
            ApplySnapshot(crossValShowingBase ? baseSnapshot : crossValSnapshots[crossValIndex]);
        }

        if (kb[chooseAKey].wasPressedThisFrame)
        {
            // Keep base
            ApplySnapshot(baseSnapshot);
            crossValIndex++;
            abSwapTimer = 0f;
            crossValShowingBase = true;
        }
        else if (kb[chooseBKey].wasPressedThisFrame)
        {
            // Adopt variant as new base
            baseSnapshot = new Dictionary<string, float>(crossValSnapshots[crossValIndex]);
            ApplySnapshot(baseSnapshot);
            crossValIndex++;
            abSwapTimer = 0f;
            crossValShowingBase = true;
        }
    }

    // ── Snapshot helpers ─────────────────────────────────────────────────────

    Dictionary<string, float> TakeSnapshot()
    {
        var snap = new Dictionary<string, float>();
        foreach (var dim in dimensionDefs)
        {
            if (dim == null) continue;
            foreach (var v in dim.variables)
            {
                string key = SnapshotKey(v);
                if (snap.ContainsKey(key)) continue;

                var field = GetField(v);
                if (field != null)
                    snap[key] = (float)field.GetValue(v.targetConfig);
            }
        }
        return snap;
    }

    void ApplySnapshot(Dictionary<string, float> snap)
    {
        foreach (var dim in dimensionDefs)
        {
            if (dim == null) continue;
            foreach (var v in dim.variables)
            {
                string key = SnapshotKey(v);
                if (snap.TryGetValue(key, out float val))
                    ApplyValue(v, val);
            }
        }
    }

    static string SnapshotKey(TuningVariable v)
    {
        return v.targetConfig != null
            ? $"{v.targetConfig.GetType().Name}.{v.fieldName}"
            : v.fieldName;
    }

    // ── ApplyValue ───────────────────────────────────────────────────────────

    void ApplyValue(TuningVariable v, float value)
    {
        var field = GetField(v);
        if (field == null)
        {
            Debug.LogError($"[TuningManager] Field '{v.fieldName}' not found on " +
                           $"{(v.targetConfig != null ? v.targetConfig.GetType().Name : "null")}");
            return;
        }

        field.SetValue(v.targetConfig, value);
        CurrentValue = value;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(v.targetConfig);
#endif

        if (v.liveSync)
            SyncLiveInstances(v);

        if (v.requiresRespawn)
            QueueRespawn(v.targetConfig);
    }

    static FieldInfo GetField(TuningVariable v)
    {
        if (v.targetConfig == null || string.IsNullOrEmpty(v.fieldName)) return null;
        return v.targetConfig.GetType().GetField(v.fieldName,
            BindingFlags.Public | BindingFlags.Instance);
    }

    // ── Live sync ────────────────────────────────────────────────────────────

    void SyncLiveInstances(TuningVariable v)
    {
        if (v.targetConfig is PlayerConfig pc)
            SyncPlayerSpringParams(pc, v.fieldName);
        else if (v.targetConfig is CentipedeConfig cc)
            SyncCentipedeSpringParams(cc, v.fieldName);
    }

    /// <summary>
    /// When any of (torsoFrequency, torsoDampingRatio, torsoMass) changes,
    /// recompute stiffness and damping and write to the live NodeWiggle on TorsoVisual.
    /// </summary>
    void SyncPlayerSpringParams(PlayerConfig pc, string fieldName)
    {
        bool isTorsoParam = fieldName == "torsoFrequency"
                         || fieldName == "torsoDampingRatio"
                         || fieldName == "torsoMass";
        if (!isTorsoParam) return;

        float stiffness = pc.TorsoStiffness;
        float damping = pc.TorsoDamping;
        float mass = pc.torsoMass;

        // Find TorsoVisual NodeWiggle — it's named "TorsoVisual" and is a child of the player root
        var player = FindAnyObjectByType<PlayerSkeletonRoot>();
        if (player == null) return;

        var torsoVisual = player.transform.Find("TorsoVisual");
        if (torsoVisual == null) return;

        var wiggle = torsoVisual.GetComponent<NodeWiggle>();
        if (wiggle == null) return;

        wiggle.stiffness = stiffness;
        wiggle.damping = damping;
        wiggle.mass = mass;
    }

    /// <summary>
    /// When any of (wiggleFrequency, wiggleDampingRatio, wiggleMass) changes,
    /// recompute stiffness and damping and write to all centipede-mode Ball instances.
    /// </summary>
    void SyncCentipedeSpringParams(CentipedeConfig cc, string fieldName)
    {
        bool isWiggleParam = fieldName == "wiggleFrequency"
                          || fieldName == "wiggleDampingRatio"
                          || fieldName == "wiggleMass";
        if (!isWiggleParam) return;

        float stiffness = cc.WiggleStiffness;
        float damping = cc.WiggleDamping;
        float mass = cc.wiggleMass;

        foreach (var ball in FindObjectsByType<Ball>(FindObjectsSortMode.None))
        {
            // Only update balls that are centipede segments (springStiffness > 0 is a
            // reasonable heuristic since free balls don't use the spring).
            // More precisely, centipede-mode balls have a kinematic RB.
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb == null || rb.bodyType != RigidbodyType2D.Kinematic) continue;

            ball.springStiffness = stiffness;
            ball.springDamping = damping;
            ball.springMass = mass;
        }
    }

    // ── Respawn ──────────────────────────────────────────────────────────────

    void QueueRespawn(ScriptableObject config)
    {
        respawnPending = true;
        respawnTargetConfig = config;
    }

    // ── Dimension spawn ──────────────────────────────────────────────────────

    EntityType GetEntityType(TuningDimensionDef dim)
    {
        if (dim?.variables == null || dim.variables.Length == 0) return EntityType.None;
        var cfg = dim.variables[0].targetConfig;
        if (cfg is PlayerConfig) return EntityType.Player;
        if (cfg is CentipedeConfig) return EntityType.Centipede;
        return EntityType.None;
    }

    void TriggerDimensionSpawn()
    {
        // Cancel any pending mid-sweep respawn — the fresh spawn supersedes it.
        respawnPending = false;

        if (activeSpawnCoroutine != null)
            StopCoroutine(activeSpawnCoroutine);

        var entityType = GetEntityType(CurrentDimension);
        CleanupOppositeEntities(entityType);

        switch (entityType)
        {
            case EntityType.Player:
                activeSpawnCoroutine = StartCoroutine(
                    AutoRespawner.SpawnFreshPlayer(playerConfig, playerAssembler, playerSpawnPosition));
                break;

            case EntityType.Centipede:
                var positions = new Vector2[centipedeSpawnCount];
                for (int i = 0; i < centipedeSpawnCount; i++)
                    positions[i] = centipedeSpawnPosition + Vector2.right * i * centipedeSpawnSpacing;
                activeSpawnCoroutine = StartCoroutine(
                    SpawnCentipedesAndUpdateCamera(centipedeConfig, centipedeAssembler, positions));
                break;
        }
    }

    /// <summary>
    /// Destroys entities that belong to the opposite type when switching dimensions,
    /// and always destroys free-flying projectile balls.
    /// </summary>
    void CleanupOppositeEntities(EntityType spawningType)
    {
        // Free balls (projectiles / ejected segments) — always clear these on any dimension switch
        foreach (var ball in FindObjectsByType<Ball>(FindObjectsSortMode.None))
        {
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
                Destroy(ball.gameObject);
        }

        switch (spawningType)
        {
            case EntityType.Centipede:
                // Destroy the player when entering a centipede dimension
                var player = FindAnyObjectByType<PlayerSkeletonRoot>();
                if (player != null)
                    Destroy(player.gameObject);
                break;

            case EntityType.Player:
                // Destroy all centipedes when entering a player dimension
                foreach (var root in FindObjectsByType<SkeletonRoot>(FindObjectsSortMode.None))
                    Destroy(root.gameObject);
                break;
        }
    }

    // ── Navigation helpers ───────────────────────────────────────────────────

    void EnterSweep()
    {
        phase = TuningPhase.Sweep;
        sweepElapsed = 0f;
        loggedValues.Clear();
        lastAppliedRespawnValue = float.NaN;
        postRespawnLockout = 0f;

        sweepStepCurrentValue = CurrentVariable.min;
        sweepStepDirection = 1;
        stepRespawnPending = false;
    }

    void EnterAB(float lo, float hi)
    {
        phase = TuningPhase.AB;
        abLo = lo;
        abHi = hi;
        abA = lo + (hi - lo) / 3f;
        abB = lo + 2f * (hi - lo) / 3f;
        abSwapTimer = 0f;
        showingA = true;
        abRoundCount = 0;
        PlayABTone(true);
        ApplyValue(CurrentVariable, abA);
    }

    void ToggleTuning()
    {
        if (phase == TuningPhase.Idle)
        {
            if (dimensionDefs == null || dimensionDefs.Length == 0)
            {
                Debug.LogWarning("[TuningManager] No dimensions defined — cannot start tuning.");
                return;
            }
            dimensionIndex = 0;
            variableIndex = 0;
            TriggerDimensionSpawn();
            EnterSweep();
            Debug.Log($"[TuningManager] Tuning started — dimension 0: {CurrentDimension?.dimensionName}");
        }
        else
        {
            phase = TuningPhase.Idle;
            ClearCameraOverride();
            Debug.Log("[TuningManager] Tuning paused.");
        }
    }

    void NextDimension()
    {
        variableIndex = 0;
        dimensionIndex++;
        if (dimensionIndex >= dimensionDefs.Length)
        {
            phase = TuningPhase.CrossValidation;
            EnterCrossValidation();
            return;
        }
        TriggerDimensionSpawn();
        EnterSweep();
        Debug.Log($"[TuningManager] Advanced to dimension {dimensionIndex}: " +
                  $"{CurrentDimension?.dimensionName}");
    }

    void PrevDimension()
    {
        variableIndex = 0;
        dimensionIndex = Mathf.Max(0, dimensionIndex - 1);
        TriggerDimensionSpawn();
        EnterSweep();
        Debug.Log($"[TuningManager] Back to dimension {dimensionIndex}: " +
                  $"{CurrentDimension?.dimensionName}");
    }

    void SkipDimension()
    {
        Debug.Log($"[TuningManager] Skipping dimension {dimensionIndex}: " +
                  $"{CurrentDimension?.dimensionName}");
        NextDimension();
    }

    void ResetCurrentDimension()
    {
        var dim = CurrentDimension;
        if (dim == null) return;

        foreach (var v in dim.variables)
            ApplyValue(v, v.defaultValue);

        TriggerDimensionSpawn();
        EnterSweep();
        Debug.Log($"[TuningManager] Reset dimension {dimensionIndex}: {dim.dimensionName}");
    }

    // ── AB Tones (testing) ───────────────────────────────────────────────────

    void PlayABTone(bool isA)
    {
        abToneSource.PlayOneShot(isA ? toneClipA : toneClipB, abToneVolume);
    }

    AudioClip GenerateToneClip(float frequency, float duration)
    {
        int sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        var clip = AudioClip.Create("ABTone", sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];
        float attackSec = 0.005f;
        float releaseSec = 0.02f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float attack  = Mathf.Clamp01(t / attackSec);
            float release = Mathf.Clamp01((duration - t) / releaseSec);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * attack * release;
        }
        clip.SetData(samples, 0);
        return clip;
    }

    // ── Camera targeting ─────────────────────────────────────────────────────

    IEnumerator SpawnCentipedesAndUpdateCamera(CentipedeConfig config, CentipedeAssembler assembler, Vector2[] positions)
    {
        yield return StartCoroutine(AutoRespawner.SpawnFreshCentipedes(config, assembler, positions));
        SetCameraForCentipede();
    }

    IEnumerator RespawnCentipedesAndUpdateCamera(CentipedeConfig config, CentipedeAssembler assembler)
    {
        yield return StartCoroutine(AutoRespawner.RespawnCentipedes(config, assembler));
        SetCameraForCentipede();
    }

    void SetCameraForCentipede()
    {
        if (followCamera == null) return;
        var root = FindAnyObjectByType<SkeletonRoot>();
        followCamera.target = root != null ? root.transform : null;
    }

    void ClearCameraOverride()
    {
        if (followCamera == null) return;
        followCamera.target = PlayerRegistry.PlayerTransform;
    }

    // ── Validation ───────────────────────────────────────────────────────────

    void ValidateDimensions()
    {
        if (dimensionDefs == null) return;

        for (int d = 0; d < dimensionDefs.Length; d++)
        {
            var dim = dimensionDefs[d];
            if (dim == null)
            {
                Debug.LogError($"[TuningManager] dimensionDefs[{d}] is null.");
                continue;
            }
            if (dim.variables == null) continue;

            for (int v = 0; v < dim.variables.Length; v++)
            {
                var tv = dim.variables[v];
                if (tv.targetConfig == null)
                {
                    Debug.LogError($"[TuningManager] {dim.dimensionName}.variables[{v}] " +
                                   $"has no targetConfig assigned.");
                    continue;
                }
                var field = tv.targetConfig.GetType().GetField(tv.fieldName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                {
                    Debug.LogError($"[TuningManager] {dim.dimensionName}.variables[{v}]: " +
                                   $"field '{tv.fieldName}' not found on " +
                                   $"{tv.targetConfig.GetType().Name}.");
                }
                else if (field.FieldType != typeof(float))
                {
                    Debug.LogWarning($"[TuningManager] {dim.dimensionName}.variables[{v}]: " +
                                     $"field '{tv.fieldName}' is {field.FieldType.Name}, " +
                                     $"expected float.");
                }
            }
        }
    }

    // ── Profile management ───────────────────────────────────────────────────

    void SaveProfile()
    {
#if UNITY_EDITOR
        string profileName = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Save playerConfig
        string playerFolder = "Assets/Configs/Tuning/Player/";
        if (!UnityEditor.AssetDatabase.IsValidFolder(playerFolder.TrimEnd('/')))
        {
            System.IO.Directory.CreateDirectory(
                System.IO.Path.Combine(Application.dataPath, "Configs/Tuning/Player"));
            UnityEditor.AssetDatabase.Refresh();
        }
        var playerClone = ScriptableObject.Instantiate(playerConfig);
        UnityEditor.AssetDatabase.CreateAsset(playerClone,
            playerFolder + playerConfig.name + "_" + profileName + ".asset");

        // Save centipedeConfig
        string centipedeFolder = "Assets/Configs/Tuning/Centipede/";
        if (!UnityEditor.AssetDatabase.IsValidFolder(centipedeFolder.TrimEnd('/')))
        {
            System.IO.Directory.CreateDirectory(
                System.IO.Path.Combine(Application.dataPath, "Configs/Tuning/Centipede"));
            UnityEditor.AssetDatabase.Refresh();
        }
        var centipedeClone = ScriptableObject.Instantiate(centipedeConfig);
        UnityEditor.AssetDatabase.CreateAsset(centipedeClone,
            centipedeFolder + centipedeConfig.name + "_" + profileName + ".asset");

        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[TuningManager] Saved profile: {profileName}");
#else
        Debug.LogWarning("[TuningManager] Profile saving is editor-only.");
#endif
    }
}
