using UnityEngine;
using System.Collections.Generic;

public class CentipedeAssembler : MonoBehaviour
{
    [Header("Pathfinding")]
    [Tooltip("Assign the player Transform to enable autonomous pathfinding. Leave null for manual/debug control.")]
    public Transform playerTarget;

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

        var nodeList = new List<SkeletonNode>();
        var ballList = new List<Ball>();

        nodeList.Add(root.GetComponent<SkeletonNode>());
        ballList.Add(SetupNodeBall(root, config));

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
            nodeList.Add(sn);
            ballList.Add(SetupNodeBall(bodyNode, config));

            previousNode = bodyNode.transform;
        }

        // Activating triggers SkeletonRoot.Awake (builds tree) then Start (seeds trails)
        root.SetActive(true);

        var controller = root.AddComponent<CentipedeController>();
        controller.Initialize(config, nodeList, ballList);

        var origMouseFollow = root.GetComponent<DebugMouseFollow>();
        if (origMouseFollow != null)
            Destroy(origMouseFollow);

        var pathfinder = root.AddComponent<CentipedePathfinder>();
        pathfinder.Initialize(config, playerTarget);

        lastSpawnedNodes  = new List<SkeletonNode>(nodeList);
        lastSpawnedBalls  = new List<Ball>(ballList);
        lastSpawnedConfig = config;

        return root;
    }

    /// <summary>
    /// Creates a Ball child on the node GameObject, configured for Centipede Mode.
    /// Returns the Ball component for registration in the segment list.
    /// </summary>
    private Ball SetupNodeBall(GameObject node, CentipedeConfig config)
    {
        BallDefinition def = config.ballDefinition != null ? config.ballDefinition : defaultBallDefinition;

        var ballGO = new GameObject("Ball");
        ballGO.transform.SetParent(node.transform, false);

        var ball = ballGO.AddComponent<Ball>();

        // Spring tuning from CentipedeConfig (computed from frequency/dampingRatio)
        ball.springStiffness = config.WiggleStiffness;
        ball.springDamping   = config.WiggleDamping;
        ball.springMass      = config.wiggleMass;

        // diameter = radius × 2 (sprite authored at 1 world-unit diameter at scale 1)
        float diameter = config.nodeRadius * 2f;

        ball.Init(def, diameter, centipedeMode: true, node: node.GetComponent<SkeletonNode>());
        ball.SetTint(config.nodeColor);

        return ball;
    }

    [Header("Test Spawn")]
    [SerializeField] private CentipedeConfig testConfig;
    [SerializeField] private Vector2 testPosition;

    [ContextMenu("Test Spawn")]
    public void TestSpawn()
    {
        if (!Application.isPlaying || testConfig == null) return;
        Spawn(testConfig, testPosition);
    }

    // ── Test Detachment ───────────────────────────────────────────────────────

    [Header("Test Detachment")]
    [Tooltip("Indices of balls (0 = head) to forcibly detach on Execute")]
    [SerializeField] private List<int> testDetachIndices = new List<int>();

    private List<SkeletonNode> lastSpawnedNodes = new List<SkeletonNode>();
    private List<Ball>         lastSpawnedBalls = new List<Ball>();
    private CentipedeConfig    lastSpawnedConfig;

    /// <summary>
    /// Injects enough upward spring velocity into each selected ball to guarantee
    /// it crosses detachDistance (3× the undamped SHM escape velocity).
    /// Right-click the component in the Inspector to invoke during Play Mode.
    /// Targeted balls turn red as visual confirmation.
    /// </summary>
    [ContextMenu("Execute Detachment Test")]
    public void ExecuteDetachmentTest()
    {
        Debug.Log($"[DetachTest] isPlaying={Application.isPlaying} | config={lastSpawnedConfig} | balls={lastSpawnedBalls.Count} | indices={testDetachIndices.Count}");

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DetachTest] Not in Play Mode.");
            return;
        }
        if (lastSpawnedConfig == null)
        {
            Debug.LogWarning("[DetachTest] lastSpawnedConfig is null — spawn via 'Test Spawn' first.");
            return;
        }
        if (lastSpawnedBalls.Count == 0)
        {
            Debug.LogWarning("[DetachTest] Ball list is empty — spawn via 'Test Spawn' first.");
            return;
        }

        foreach (int i in testDetachIndices)
        {
            if (i < 0 || i >= lastSpawnedBalls.Count)
            {
                Debug.LogWarning($"[DetachTest] Index {i} out of range (ball count: {lastSpawnedBalls.Count})");
                continue;
            }
            Ball ball = lastSpawnedBalls[i];
            if (ball == null)
            {
                Debug.LogWarning($"[DetachTest] Ball[{i}] is null (already detached?)");
                continue;
            }

            float launchSpeed = lastSpawnedConfig.detachDistance
                                * Mathf.Sqrt(ball.springStiffness / ball.springMass) * 3f;
            ball.InjectSpringVelocity(Vector2.up * launchSpeed);
            ball.SetTint(Color.red);
            Debug.Log($"[DetachTest] Ball[{i}] launched at {launchSpeed:F1} m/s upward (red tint applied).");
        }
    }
}
