using UnityEngine;

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

    public override void OnLaunch(Ball ball, Rigidbody2D rb, Vector2 launchVelocity)
    {
        if (fireTrailPrefab != null)
        {
            GameObject trail = Instantiate(fireTrailPrefab, ball.transform.position, Quaternion.identity);
            trail.AddComponent<TrailCleanup>();

            // Store the trail on the ball itself so each instance tracks its own
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

        Collider2D[] hits = Physics2D.OverlapCircleAll(hitPoint, explosionRadius);
        foreach (Collider2D hit in hits)
        {
            Rigidbody2D hitRb = hit.GetComponent<Rigidbody2D>();
            if (hitRb != null)
            {
                Vector2 direction = (hitRb.position - hitPoint).normalized;
                float distance = Vector2.Distance(hitRb.position, hitPoint);
                float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                hitRb.AddForce(direction * explosionForce * falloff, ForceMode2D.Impulse);
            }
        }

        // Retrieve this ball's own trail and clean it up
        BallState state = ball.gameObject.GetComponent<BallState>();
        if (state != null && state.trail != null)
        {
            state.trail.GetComponent<TrailCleanup>()?.StopAndDestroy(3f);
            state.trail = null;
        }

        ball.DestroySelf();
    }
}