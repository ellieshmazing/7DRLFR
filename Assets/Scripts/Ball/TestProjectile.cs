using UnityEngine;

/// <summary>
/// Moves in a straight line; explodes on contact with tilemaps or after 1 second.
/// Spawned at runtime by TestProjectileLauncher.
/// </summary>
public class TestProjectile : MonoBehaviour
{
    private Vector2 velocity;
    private GameObject explosionPrefab;
    private float lifetime = 1f;
    private bool exploded = false;
    private LayerMask excludedLayers;
  

    public void Init(Vector2 vel, GameObject explosion, Sprite sprite, float size, LayerMask excluded, float gravity)
    {
        excludedLayers = excluded;
        velocity = vel;
        explosionPrefab = explosion;

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 10;
        transform.localScale = Vector3.one * size;

        // Add a CircleCollider2D sized to match the sprite
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = size * 0.06f;
        col.isTrigger = true;

        // Rigidbody2D required for trigger callbacks to fire
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = gravity;
        rb.linearVelocity = vel;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        // Keep moving manually (rb.velocity handles physics movement,
        // but we still count down lifetime here)
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Explode();
    }

    // Tilemap colliders are solid, so use OnTriggerEnter2D
    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & excludedLayers) != 0) return;
        // Check if we hit a tilemap layer (or any collider you want)
        if (other.CompareTag("Ground") || other.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>() != null)
        {
            Explode();
        }
    }

    // Fallback: also catches non-trigger tilemap colliders
    void OnCollisionEnter2D(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & excludedLayers) != 0) return;

        if (col.gameObject.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>() != null)
        {
            Explode();
        }
    }

    void Explode()
    {
        if (exploded) return;   // guard against double-trigger
        exploded = true;
        Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}