using UnityEngine;

/// <summary>
/// Singleton that maintains the player's spatiotemporal scent trail.
///
/// The field is a sum of time-decaying Gaussians — one per sampled player position.
/// Each sample starts with weight 1.0 and can be locally suppressed ("consumed")
/// as a ScentFieldNavigator passes through it.
///
/// All ScentFieldNavigator instances in the scene share this one field.
/// Created lazily via ScentField.GetOrCreate() — do not place manually in the scene.
/// </summary>
public class ScentField : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    public static ScentField Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Sample storage ────────────────────────────────────────────────────────

    private struct Sample
    {
        public Vector2 position;
        public float   timestamp;
        public float   weight;
    }

    private Transform player;
    private Sample[]  samples;
    private int       head;          // next write index
    private int       count;         // valid samples currently in buffer
    private float     lastSampleTime;

    // ── Config (copied from CentipedeConfig on first Initialize) ─────────────

    private int   historySize;
    private float sampleInterval;
    private float decayTime;         // e^(-age/decayTime) → 37% at t=decayTime
    private float sigma;             // Gaussian spatial spread in world units

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the existing singleton, or creates one on a new GameObject.
    /// </summary>
    public static ScentField GetOrCreate()
    {
        if (Instance != null) return Instance;
        return new GameObject("[ScentField]").AddComponent<ScentField>();
    }

    /// <summary>
    /// Sets up the player reference and field parameters from the config.
    /// Safe to call multiple times — only the first call takes effect so that all
    /// centipedes in a scene share a single consistent field.
    /// </summary>
    public void Initialize(Transform playerTransform, CentipedeConfig cfg)
    {
        if (player != null) return; // already initialized — reuse

        player         = playerTransform;
        historySize    = cfg.scentHistorySize;
        sampleInterval = cfg.scentSampleInterval;
        decayTime      = cfg.scentDecayTime;
        sigma          = cfg.scentSigma;

        samples        = new Sample[historySize];
        head           = 0;
        count          = 0;
        lastSampleTime = Time.time - sampleInterval; // ensure first sample fires immediately
    }

    // ── Per-frame emission ────────────────────────────────────────────────────

    void Update()
    {
        if (player == null || samples == null) return;
        if (Time.time - lastSampleTime < sampleInterval) return;

        samples[head] = new Sample
        {
            position  = player.position,
            timestamp = Time.time,
            weight    = 1f
        };
        head           = (head + 1) % historySize;
        count          = Mathf.Min(count + 1, historySize);
        lastSampleTime = Time.time;
    }

    // ── Public field API ──────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates scent field intensity at a world position.
    /// Returns the sum of time-decaying Gaussians from all stored player samples.
    /// O(count) — call sparingly per frame when count is large.
    /// </summary>
    public float Evaluate(Vector2 pos)
    {
        if (samples == null || count == 0) return 0f;

        float t        = Time.time;
        float twoSigSq = 2f * sigma * sigma;
        float value    = 0f;

        for (int i = 0; i < count; i++)
        {
            ref Sample s = ref samples[i];
            if (s.weight < 0.001f) continue;

            float age     = t - s.timestamp;
            float temporal = Mathf.Exp(-age / decayTime);
            if (temporal < 0.001f) continue;

            float distSq  = (pos - s.position).sqrMagnitude;
            float spatial  = Mathf.Exp(-distSq / twoSigSq);
            value += s.weight * temporal * spatial;
        }

        return value;
    }

    /// <summary>
    /// Reduces weights of scent samples near <paramref name="pos"/>.
    /// Suppression is strongest at the centipede's center and falls off linearly to zero at
    /// <paramref name="radius"/>. Call once per FixedUpdate from ScentFieldNavigator.
    /// </summary>
    public void Consume(Vector2 pos, float rate, float radius, float deltaTime)
    {
        if (samples == null || count == 0) return;

        float radiusSq = radius * radius;

        for (int i = 0; i < count; i++)
        {
            ref Sample s = ref samples[i];
            if (s.weight < 0.001f) continue;

            float distSq = (pos - s.position).sqrMagnitude;
            if (distSq >= radiusSq) continue;

            float proximity = 1f - distSq / radiusSq; // 1 at center, 0 at edge
            s.weight = Mathf.Max(0f, s.weight - rate * proximity * deltaTime);
        }
    }

    /// <summary>
    /// Discards all scent samples. Call when the player respawns to erase the old trail.
    /// </summary>
    public void Clear()
    {
        count = 0;
        head  = 0;
    }
}
