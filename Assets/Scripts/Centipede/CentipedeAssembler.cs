using UnityEngine;

public class CentipedeAssembler : MonoBehaviour
{
    [Header("Default Prefabs")]
    [Tooltip("Default prefab for the head node (should have SkeletonRoot or it will be added)")]
    public GameObject defaultHeadPrefab;

    [Tooltip("Default prefab for body nodes (should have SkeletonNode or it will be added)")]
    public GameObject defaultBodyPrefab;

    [Header("Default Ball")]
    [Tooltip("BallDefinition used when a config has no ballDefinition assigned")]
    public BallDefinition defaultBallDefinition;

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

        SetupNodeBall(root, config);

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
            SetupNodeBall(bodyNode, config);

            previousNode = bodyNode.transform;
        }

        // Activating triggers SkeletonRoot.Awake (builds tree) then Start (seeds trails)
        root.SetActive(true);

        return root;
    }

    /// <summary>
    /// Creates a Ball child on the node GameObject, configured for Centipede Mode.
    /// The Ball replaces the old Visual child (SpriteRenderer + CircleCollider2D + NodeWiggle).
    /// </summary>
    private void SetupNodeBall(GameObject node, CentipedeConfig config)
    {
        BallDefinition def = config.ballDefinition != null ? config.ballDefinition : defaultBallDefinition;

        var ballGO = new GameObject("Ball");
        ballGO.transform.SetParent(node.transform, false);

        var ball = ballGO.AddComponent<Ball>();

        // Spring tuning from CentipedeConfig
        ball.springStiffness = config.wiggleStiffness;
        ball.springDamping   = config.wiggleDamping;
        ball.springMass      = config.wiggleMass;

        // diameter = radius × 2 (sprite authored at 1 world-unit diameter at scale 1)
        float diameter = config.nodeRadius * 2f;

        ball.Init(def, diameter, centipedeMode: true, node: node.GetComponent<SkeletonNode>());
        ball.SetTint(config.nodeColor);
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
