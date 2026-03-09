using UnityEngine;

/// <summary>
/// Minimal game loop: spawns the player on start, then spawns centipedes above the
/// camera at an increasing rate as the player's furthest rightward position grows.
///
/// Spawn rate formula: interval = baseInterval / (1 + maxPlayerX * progressScale)
/// clamped to minInterval. The player's X progress is one-way — moving left never
/// reduces the spawn rate.
/// </summary>
public sealed class GameLoop : MonoBehaviour
{
    [Header("Assemblers & Configs")]
    [SerializeField] private PlayerAssembler playerAssembler;
    [SerializeField] private PlayerConfig playerConfig;
    [SerializeField] private CentipedeAssembler centipedeAssembler;
    [SerializeField] private CentipedeConfig centipedeConfig;

    [Header("Player Spawn")]
    [SerializeField] private Vector2 playerSpawnPosition = new(0f, 1f);

    [Header("Centipede Spawning")]
    [Tooltip("Spawn interval (seconds) at x = 0. Default: 1 centipede every 2 minutes.")]
    [SerializeField] private float baseInterval = 120f;

    [Tooltip("Minimum spawn interval regardless of how far right the player has gone.")]
    [SerializeField] private float minInterval = 5f;

    [Tooltip("Controls how quickly rightward progress shortens the interval. " +
             "interval = baseInterval / (1 + maxPlayerX * progressScale).")]
    [SerializeField] private float progressScale = 0.05f;

    [Tooltip("World units above the camera's top edge where centipedes are spawned.")]
    [SerializeField] private float spawnAboveCamera = 2f;

    private float _spawnTimer;
    private float _maxPlayerX;

    private void Start()
    {
        playerAssembler.Spawn(playerConfig, playerSpawnPosition);
        centipedeAssembler.playerTarget = PlayerRegistry.PlayerTransform;
        SpawnCentipede();
        _spawnTimer = baseInterval;
    }

    private void Update()
    {
        TrackProgress();

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            SpawnCentipede();
            _spawnTimer = CurrentInterval();
        }
    }

    private void TrackProgress()
    {
        Transform player = PlayerRegistry.PlayerTransform;
        if (player == null) return;

        if (player.position.x > _maxPlayerX)
            _maxPlayerX = player.position.x;
    }

    private float CurrentInterval() =>
        Mathf.Max(baseInterval / (1f + _maxPlayerX * progressScale), minInterval);

    private void SpawnCentipede()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float halfH   = cam.orthographicSize;
        float halfW   = halfH * cam.aspect;
        Vector3 camPos = cam.transform.position;

        float spawnX = Random.Range(camPos.x - halfW, camPos.x + halfW);
        float spawnY = camPos.y + halfH + spawnAboveCamera;

        centipedeAssembler.Spawn(centipedeConfig, new Vector2(spawnX, spawnY));
    }
}
