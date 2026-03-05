using UnityEngine;

/// <summary>
/// Moves in a straight line; after 1 second spawns PS_Explosion and destroys itself.
/// Spawned at runtime by TestProjectileLauncher.
/// </summary>
public class TestProjectile : MonoBehaviour
{
    private Vector2 velocity;
    private GameObject explosionPrefab;
    private float lifetime = 1f;

    public void Init(Vector2 vel, GameObject explosion, Sprite sprite, float size)
    {
        velocity        = vel;
        explosionPrefab = explosion;

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = 10;
        transform.localScale = Vector3.one * size;
    }

    void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Explode();
    }

    void Explode()
    {
        Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }
}
