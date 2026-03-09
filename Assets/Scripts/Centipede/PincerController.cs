using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animates two mirrored pincer sprites on the centipede head with a sinusoidal
/// clapping motion. Static trigger hitboxes (decoupled from the sprite rotation)
/// handle kill-zone detection and dispatch through IPlayerHitEffect.
///
/// Built at runtime by CentipedeAssembler. Call Build() immediately after
/// AddComponent while the root is active.
///
/// OnClick fires once per cycle at the moment the pincers snap fully closed.
/// Subscribe for audio, particles, or screen shake.
/// </summary>
[DefaultExecutionOrder(0)]
public class PincerController : MonoBehaviour
{
    public event System.Action OnClick;

    private CentipedeConfig config;
    private Transform leftPincer;
    private Transform rightPincer;
    private float phase;
    private float prevSin;
    private float currentClickSpeed;
    private readonly List<IPlayerHitEffect> hitEffects = new List<IPlayerHitEffect>();

    /// <summary>
    /// Creates pincer sprite GOs and hitbox GOs as children of this GO.
    /// Skips setup if config.pincerSprite is null.
    /// </summary>
    public void Build(CentipedeConfig cfg, Sprite sprite)
    {
        config = cfg;
        if (sprite == null) return;

        leftPincer  = CreatePincerVisual("LeftPincer",  flipX: false, cfg, sprite);
        rightPincer = CreatePincerVisual("RightPincer", flipX: true,  cfg, sprite);

        CreateHitbox("LeftHitbox",  new Vector3(-cfg.pincerHitboxOffsetX, cfg.pincerHitboxOffsetY, 0f), cfg);
        CreateHitbox("RightHitbox", new Vector3( cfg.pincerHitboxOffsetX, cfg.pincerHitboxOffsetY, 0f), cfg);

        hitEffects.Add(new DestroyPlayerEffect());
    }

    private Transform CreatePincerVisual(string goName, bool flipX, CentipedeConfig cfg, Sprite sprite)
    {
        float xSign = flipX ? 1f : -1f;

        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(xSign * cfg.pincerOffsetX, cfg.pincerOffsetY, 0f);
        go.transform.localScale    = Vector3.one * cfg.pincerSize;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.flipX  = flipX;

        return go.transform;
    }

    private void CreateHitbox(string goName, Vector3 localPos, CentipedeConfig cfg)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;

        var box    = go.AddComponent<BoxCollider2D>();
        box.size      = cfg.pincerColliderSize;
        box.isTrigger = true;

        var rb        = go.AddComponent<Rigidbody2D>();
        rb.bodyType   = RigidbodyType2D.Kinematic;
        rb.simulated  = true;

        var detector       = go.AddComponent<PincerHitDetector>();
        detector.controller = this;
    }

    void FixedUpdate()
    {
        if (leftPincer == null) return;

        UpdateClickSpeed();
        UpdateAnimation();
    }

    private void UpdateClickSpeed()
    {
        if (PlayerRegistry.PlayerTransform == null)
        {
            currentClickSpeed = config.idleClickSpeed;
            return;
        }

        float dist      = Vector2.Distance(transform.position, PlayerRegistry.PlayerTransform.position);
        float outerRange = Mathf.Max(0.001f, config.attackOuterRadius - config.attackInnerRadius);
        float t          = 1f - Mathf.Clamp01((dist - config.attackInnerRadius) / outerRange);
        currentClickSpeed = Mathf.Lerp(config.idleClickSpeed, config.attackClickSpeed, t);
    }

    private void UpdateAnimation()
    {
        phase += currentClickSpeed * 2f * Mathf.PI * Time.fixedDeltaTime;
        phase %= 2f * Mathf.PI;

        float sinVal = Mathf.Sin(phase);
        float angle  = sinVal * config.clickAngle;

        leftPincer.localEulerAngles  = new Vector3(0f, 0f, +angle);
        rightPincer.localEulerAngles = new Vector3(0f, 0f, -angle);

        // Fire OnClick at the closing zero-crossing (sin positive→negative in second half of cycle)
        if (prevSin > 0f && sinVal <= 0f && phase > Mathf.PI)
            OnClick?.Invoke();

        prevSin = sinVal;
    }

    /// <summary>
    /// Called by PincerHitDetector when a player collider enters a hitbox trigger.
    /// Iterates hitEffects in order; breaks early if a prior effect destroyed the player.
    /// </summary>
    public void HandlePlayerHit(GameObject playerGO)
    {
        if (playerGO == null) return;

        foreach (IPlayerHitEffect effect in hitEffects)
        {
            if (playerGO == null) break;
            effect.Apply(playerGO);
        }
    }
}
