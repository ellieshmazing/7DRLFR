using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to the Player root. Assign arm and explosionPrefab in the Inspector.
/// Left-click fires a projectile from the arm position, aimed away from the torso.
/// </summary>
public class TestProjectileLauncher : MonoBehaviour
{
    [SerializeField] private LayerMask projectileExcludedLayers;
    [SerializeField] private float projecileOffset;
    [SerializeField] private float gravity;
    public PlayerArmController arm;
    public GameObject explosionPrefab;
    public float speed = 8f;

    [Tooltip("Sprite shown on the flying projectile. Leave empty to use Unity's built-in circle.")]
    public Sprite projectileSprite;
    public float projectileSize = 0.2f;

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Fire();
    }

    void Fire()
    {
        if (arm == null || explosionPrefab == null) return;

        Vector2 firePos  = arm.transform.position;
        Vector2 torsoPos = arm.torsoVisual.position;
        Vector2 dir      = (firePos - torsoPos).normalized;

        var go = new GameObject("TestProjectile");
        go.transform.position = firePos + dir * projecileOffset;

        var sprite = projectileSprite != null
            ? projectileSprite
            : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");

        go.AddComponent<TestProjectile>().Init(dir * speed, explosionPrefab, sprite, projectileSize, projectileExcludedLayers, gravity);
    }
}
