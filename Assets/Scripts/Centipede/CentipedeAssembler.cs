using UnityEngine;

public class CentipedeAssembler : MonoBehaviour
{
    [Header("Default Prefabs")]
    [Tooltip("Default prefab for the head node (should have SkeletonRoot or it will be added)")]
    public GameObject defaultHeadPrefab;

    [Tooltip("Default prefab for body nodes (should have SkeletonNode or it will be added)")]
    public GameObject defaultBodyPrefab;

    [Header("Default Visuals")]
    [Tooltip("Circle sprite used when a node has no SpriteRenderer (should be 1 world unit diameter at scale 1)")]
    public Sprite defaultNodeSprite;

    /// <summary>
    /// Spawns a centipede from a config asset at the given world position.
    /// Returns the root GameObject.
    /// </summary>
    public GameObject Spawn(CentipedeConfig config, Vector2 position)
    {
        GameObject headPrefab = config.headPrefab != null ? config.headPrefab : defaultHeadPrefab;
        GameObject bodyPrefab = config.bodyPrefab != null ? config.bodyPrefab : defaultBodyPrefab;

        // Create root inactive so Awake doesn't fire until the full hierarchy is built
        GameObject root = Instantiate(headPrefab, (Vector3)position, Quaternion.identity);
        root.SetActive(false);
        root.name = "Centipede";

        if (root.GetComponent<SkeletonRoot>() == null)
            root.AddComponent<SkeletonRoot>();

        SetupNodeVisual(root, config);

        // Chain body nodes: each is a Unity child of the previous,
        // so BuildTreeFromHierarchy discovers the full chain on Awake
        Transform previousNode = root.transform;

        for (int i = 1; i < config.nodeCount; i++)
        {
            Vector2 nodePos = position - Vector2.up * (config.followDistance * i);
            GameObject bodyNode = Instantiate(bodyPrefab, (Vector3)nodePos, Quaternion.identity, previousNode);
            bodyNode.name = $"Node_{i}";

            SkeletonNode sn = bodyNode.GetComponent<SkeletonNode>();
            if (sn == null)
                sn = bodyNode.AddComponent<SkeletonNode>();

            sn.followDistance = config.followDistance;
            SetupNodeVisual(bodyNode, config);

            previousNode = bodyNode.transform;
        }

        // Activating triggers SkeletonRoot.Awake (builds tree) then Start (seeds trails)
        root.SetActive(true);

        return root;
    }

    /// <summary>
    /// Ensures a node has a visible circle sprite, collider, and wiggle component
    /// on a Visual child, sized and colored according to the config.
    /// </summary>
    private void SetupNodeVisual(GameObject node, CentipedeConfig config)
    {
        float radius = config.nodeRadius;
        Color color = config.nodeColor;

        Sprite sprite = config.nodeSprite != null ? config.nodeSprite : defaultNodeSprite;

        // SpriteRenderer — reuse existing or create a visual child
        var sr = node.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(node.transform, false);
            sr = visual.AddComponent<SpriteRenderer>();
        }

        sr.sprite = sprite;

        sr.color = color;

        // Scale the visual child to match diameter (sprite is 1 world unit at scale 1)
        sr.transform.localScale = Vector3.one * radius * 2f;

        GameObject visualObj = sr.gameObject;

        // Collider — on the Visual child so it wiggles with the sprite
        var collider = visualObj.GetComponent<CircleCollider2D>();
        if (collider == null)
            collider = visualObj.AddComponent<CircleCollider2D>();
        // Radius is in local space; the visual is scaled by diameter,
        // so a local radius of 0.5 maps to nodeRadius in world units
        collider.radius = 0.5f;

        // Kinematic Rigidbody2D required for the collider to function
        var rb = visualObj.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = visualObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        // Spring wiggle — gives the visual inertia and momentum
        var wiggle = visualObj.GetComponent<NodeWiggle>();
        if (wiggle == null)
            wiggle = visualObj.AddComponent<NodeWiggle>();
        wiggle.stiffness = config.wiggleStiffness;
        wiggle.damping = config.wiggleDamping;
        wiggle.mass = config.wiggleMass;
    }

    [Header("Test")]
    [SerializeField] private CentipedeConfig testConfig;
    [SerializeField] private Vector2 testPosition;

    [ContextMenu("Test Spawn")]
    private void TestSpawn()
    {
        if (!Application.isPlaying || testConfig == null) return;
        Spawn(testConfig, testPosition);
    }
}
