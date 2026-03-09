using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "FireballEffect", menuName = "Ball Effects/Fireball")]
public class FireballEffect : BallEffect
{
    [Header("Launch Settings")]
    public GameObject fireTrailPrefab;
    public AudioClip launchSound;
    [Range(0f, 1f)] public float launchVolume = 0.8f;

    [Header("Collision Settings")]
    public GameObject explosionPrefab;
    public AudioClip impactSound;
    [Range(0f, 1f)] public float impactVolume = 1f;
    public float explosionRadius = 1.5f;
    public float explosionForce = 10f;

    [Header("Tilemap Destruction")]
    [Tooltip("Tag shared by all destructible Tilemap GameObjects in the scene.")]
    public string tilemapTag = "Destructible";
    [Tooltip("Particle effect spawned at each broken tile position.")]
    public GameObject tileBreakPrefab;
    [Tooltip("Max random delay in seconds between each tile break effect firing.")]
    public float tileBreakMaxDelay = 0.05f;

    public override void OnLaunch(Ball ball, Rigidbody2D rb, Vector2 launchVelocity)
    {
        if (fireTrailPrefab != null)
        {
            GameObject trail = Instantiate(fireTrailPrefab, ball.transform.position, Quaternion.identity);
            trail.AddComponent<TrailCleanup>();

            BallState state = ball.gameObject.AddComponent<BallState>();
            state.trail = trail;
        }

        if (launchSound != null)
            AudioSource.PlayClipAtPoint(launchSound, ball.transform.position, launchVolume);
    }

    public override void OnUpdate(Ball ball)
    {
        BallState state = ball.gameObject.GetComponent<BallState>();
        if (state != null && state.trail != null)
            state.trail.transform.position = ball.transform.position;
    }

    public override void OnCollision(Ball ball, Collision2D collision)
    {
        Vector2 hitPoint = collision.contacts[0].point;

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, hitPoint, Quaternion.identity);
            Destroy(explosion, 3f);
        }

        if (impactSound != null)
            AudioSource.PlayClipAtPoint(impactSound, hitPoint, impactVolume);

        // Physics force on nearby rigidbodies
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint, explosionRadius);
        foreach (Collider2D hit in hits)
        {
            Rigidbody2D hitRb = hit.GetComponent<Rigidbody2D>();
            if (hitRb != null)
            {
                Vector2 direction = (hitRb.position - hitPoint).normalized;
                float distance = Vector2.Distance(hitRb.position, hitPoint);
                float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                float impulse = explosionForce * falloff;

                if (hitRb.bodyType == RigidbodyType2D.Kinematic)
                {
                    // Kinematic bodies ignore AddForce — inject into the Ball spring velocity instead
                    Ball centipedeBall = hit.GetComponent<Ball>();
                    centipedeBall?.InjectSpringVelocity(direction * impulse / hitRb.mass);
                }
                else
                {
                    hitRb.AddForce(direction * impulse, ForceMode2D.Impulse);
                }
            }
        }

        // Erase tiles within explosion radius on all tagged Tilemaps
        foreach (GameObject tilemapObj in GameObject.FindGameObjectsWithTag(tilemapTag))
        {
            Tilemap tilemap = tilemapObj.GetComponent<Tilemap>();
            if (tilemap == null) continue;

            Vector3 cellSize = tilemap.cellSize;

            for (float x = hitPoint.x - explosionRadius; x <= hitPoint.x + explosionRadius; x += cellSize.x)
            {
                for (float y = hitPoint.y - explosionRadius; y <= hitPoint.y + explosionRadius; y += cellSize.y)
                {
                    Vector2 samplePoint = new Vector2(x, y);
                    if (Vector2.Distance(samplePoint, hitPoint) > explosionRadius) continue;

                    Vector3Int cellPos = tilemap.WorldToCell(new Vector3(samplePoint.x, samplePoint.y, 0f));
                    if (tilemap.HasTile(cellPos))
                    {
                        if (tileBreakPrefab != null)
                        {
                            Vector3 tileWorldCenter = tilemap.GetCellCenterWorld(cellPos);
                            Color tileColor = SampleTileColor(tilemap, cellPos);
                            float delay = Random.Range(0f, tileBreakMaxDelay);
                            SpawnTileBreakEffect(tileWorldCenter, delay, tileColor);
                        }

                        tilemap.SetTile(cellPos, null);
                    }
                }
            }
        }

        // Clean up trail
        BallState state = ball.gameObject.GetComponent<BallState>();
        if (state != null && state.trail != null)
        {
            state.trail.GetComponent<TrailCleanup>()?.StopAndDestroy(3f);
            state.trail = null;
        }

        ball.DestroySelf();
    }

    /// <summary>
    /// Samples the color of a tile by reading its sprite texture at the tile's center UV.
    /// Falls back to the Tilemap's tint color, then white if no sprite is found.
    /// </summary>
    private Color SampleTileColor(Tilemap tilemap, Vector3Int cellPos)
    {
        TileBase tileBase = tilemap.GetTile(cellPos);

        // Try to get the sprite from the tile
        Sprite sprite = null;
        if (tileBase is Tile tile)
            sprite = tile.sprite;

        if (sprite != null)
        {
            Texture2D tex = sprite.texture;

            // Convert sprite's pivot-relative rect to texture pixel coords
            Rect rect = sprite.textureRect;
            int px = Mathf.RoundToInt(rect.x + rect.width * 0.5f);
            int py = Mathf.RoundToInt(rect.y + rect.height * 0.5f);

            // GetPixel requires a readable texture; falls through to tint if not readable
            try
            {
                Color texColor = tex.GetPixel(px, py);
                Color tint = tilemap.GetColor(cellPos);
                Color finalColor = texColor * tint;
                Debug.Log($"[TileColor] Cell {cellPos} | Sprite: {sprite.name} | TexColor: {texColor} | Tint: {tint} | Final: {finalColor}");
                return finalColor;
            }
            catch (System.Exception e)
            {
                // Texture is not read/write enabled � fall back to tint color
                Debug.LogWarning($"[TileColor] GetPixel failed on '{tex.name}' (not Read/Write enabled?): {e.Message}");
            }
        }

        // Fallback: use the tilemap's color tint for this cell
        Color cellTint = tilemap.GetColor(cellPos);
        return cellTint == Color.clear ? Color.white : cellTint;
    }

    private void SpawnTileBreakEffect(Vector3 position, float delay, Color tileColor)
    {
        CoroutineRunner.Instance.StartCoroutine(SpawnAfterDelay(position, delay, tileColor));
    }

    private System.Collections.IEnumerator SpawnAfterDelay(Vector3 position, float delay, Color tileColor)
    {
        yield return new WaitForSeconds(delay);

        if (tileBreakPrefab != null)
        {
            GameObject tileBreak = Instantiate(tileBreakPrefab, position, Quaternion.identity);

            // Apply the sampled tile color to all particle systems on the prefab
            foreach (ParticleSystem ps in tileBreak.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(tileColor);
            }

            // Auto-destroy after all particle systems finish
            ParticleSystem[] systems = tileBreak.GetComponentsInChildren<ParticleSystem>();
            float longestDuration = 0f;
            foreach (ParticleSystem ps in systems)
                longestDuration = Mathf.Max(longestDuration, ps.main.duration + ps.main.startLifetime.constantMax);

            Destroy(tileBreak, longestDuration);
        }
    }
}