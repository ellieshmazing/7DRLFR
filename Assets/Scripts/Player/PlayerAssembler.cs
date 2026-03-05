using UnityEngine;

/// <summary>
/// Instantiates all GameObjects and components that make up the player
/// character at runtime, mirroring the pattern of CentipedeAssembler.
///
/// Hierarchy created by Spawn():
///
///   Player  (PlayerSkeletonNode [root], PlayerSkeletonRoot, Rigidbody2D [gravityScale=0])
///   ├── TorsoVisual      (SpriteRenderer, NodeWiggle)
///   ├── Arm              (SpriteRenderer, PlayerArmController)
///   ├── HipNode          (PlayerSkeletonNode, PlayerHipNode, PlayerFeet)
///   ├── LeftFootVisual   (SpriteRenderer, CircleCollider2D, Rigidbody2D [Dynamic, gravity])
///   └── RightFootVisual  (SpriteRenderer, CircleCollider2D, Rigidbody2D [Dynamic, gravity])
///
/// Foot visuals are siblings of HipNode (children of root), not children of HipNode.
/// Making Dynamic-RB objects children of a moving plain Transform causes per-frame
/// Transform drift when the parent moves; sibling placement avoids this entirely.
/// PlayerHipNode and PlayerFeet hold direct RB references instead.
/// </summary>
public class PlayerAssembler : MonoBehaviour
{
    [Header("Fallback Sprite")]
    [Tooltip("Circle sprite used when a config slot has no sprite assigned " +
             "(should be 1 world unit diameter at scale 1)")]
    public Sprite defaultSprite;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Spawns a fully assembled player at <paramref name="position"/>.
    /// Returns the root GameObject.
    /// </summary>
    public GameObject Spawn(PlayerConfig config, Vector2 position)
    {
        // Keep inactive until the full hierarchy is built so Awake/Start fire
        // only after all components are present.
        var root = new GameObject("Player");
        root.SetActive(false);
        root.transform.position = (Vector3)position;

        // --- Torso physics body ---
        var torsoRB = root.AddComponent<Rigidbody2D>();
        torsoRB.gravityScale  = 0f;
        torsoRB.linearDamping = 3f;
        torsoRB.constraints   = RigidbodyConstraints2D.FreezeRotation;

        // --- Torso skeleton node (root of gizmo tree) ---
        var torsoNode = root.AddComponent<PlayerSkeletonNode>();
        torsoNode.gizmoColor = config.torsoNodeGizmoColor;

        // --- Skeleton root / movement controller ---
        var playerRoot = root.AddComponent<PlayerSkeletonRoot>();
        playerRoot.moveForce   = config.moveForce;
        playerRoot.maxSpeed    = config.maxSpeed;
        playerRoot.standHeight = config.standHeight;

        // --- Torso visual (wiggles around torso node) ---
        var torsoVisualGO = new GameObject("TorsoVisual");
        torsoVisualGO.transform.SetParent(root.transform, false);
        torsoVisualGO.transform.localPosition = Vector3.zero;
        torsoVisualGO.transform.localScale    = Vector3.one * config.torsoRadius * 2f;

        var torsoSR = torsoVisualGO.AddComponent<SpriteRenderer>();
        torsoSR.sprite       = config.torsoSprite != null ? config.torsoSprite : defaultSprite;
        torsoSR.color        = config.torsoColor;
        torsoSR.sortingOrder = config.torsoSortingOrder;

        var torsoWiggle = torsoVisualGO.AddComponent<NodeWiggle>();
        torsoWiggle.stiffness = config.torsoWiggleStiffness;
        torsoWiggle.damping   = config.torsoWiggleDamping;
        torsoWiggle.mass      = config.torsoWiggleMass;

        // --- Arm (orbits torso visual, points at mouse) ---
        var armGO = new GameObject("Arm");
        armGO.transform.SetParent(root.transform, false);
        armGO.transform.localPosition = Vector3.zero;
        armGO.transform.localScale    = Vector3.one * config.armScale;

        var armSR = armGO.AddComponent<SpriteRenderer>();
        armSR.sprite       = config.armSprite != null ? config.armSprite : defaultSprite;
        armSR.color        = config.armColor;
        armSR.sortingOrder = config.armSortingOrder;

        var armCtrl = armGO.AddComponent<PlayerArmController>();
        armCtrl.torsoVisual = torsoVisualGO.transform;
        armCtrl.orbitRadius = config.armOrbitRadius;

        // --- Hip node ---
        // footOffsetY places it below the torso at spawn (negative value expected).
        // After the first FixedUpdate PlayerHipNode takes over Y from foot positions.
        Vector2 hipSpawnPos = new Vector2(position.x, position.y + config.footOffsetY);

        var hipGO = new GameObject("HipNode");
        hipGO.transform.SetParent(root.transform, worldPositionStays: false);
        hipGO.transform.position = (Vector3)hipSpawnPos;

        var hipSkelNode = hipGO.AddComponent<PlayerSkeletonNode>();
        hipSkelNode.gizmoColor  = config.footNodeGizmoColor;
        hipSkelNode.localOffset = new Vector2(0f, config.footOffsetY);

        var hipNodeScript = hipGO.AddComponent<PlayerHipNode>();

        var feetScript       = hipGO.AddComponent<PlayerFeet>();
        feetScript.footSpreadX = config.footSpreadX;
        feetScript.stiffness   = config.footWiggleStiffness;
        feetScript.damping     = config.footWiggleDamping;
        feetScript.mass        = config.footWiggleMass;

        // Wire hip node into PlayerSkeletonRoot
        playerRoot.hipNode = hipGO.transform;

        // --- Foot visuals (siblings of HipNode, direct children of root) ---
        float footSpawnY = hipSpawnPos.y;

        var leftFootRB  = CreateFootVisual("LeftFootVisual",  root.transform,
                              new Vector2(position.x - config.footSpreadX, footSpawnY), config);
        var rightFootRB = CreateFootVisual("RightFootVisual", root.transform,
                              new Vector2(position.x + config.footSpreadX, footSpawnY), config);

        // Wire foot RBs into both hip-node scripts
        hipNodeScript.leftFootRB  = leftFootRB;
        hipNodeScript.rightFootRB = rightFootRB;
        feetScript.leftFootRB     = leftFootRB;
        feetScript.rightFootRB    = rightFootRB;

        // Activating triggers Awake then Start on the complete hierarchy
        root.SetActive(true);

        return root;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Rigidbody2D CreateFootVisual(
        string goName, Transform parent,
        Vector2 worldPos, PlayerConfig config)
    {
        var go = new GameObject(goName);
        go.layer = LayerMask.NameToLayer("Player");
        go.transform.SetParent(parent, worldPositionStays: true);
        go.transform.position   = (Vector3)worldPos;
        go.transform.localScale = Vector3.one * config.footRadius * 2f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = config.footSprite != null ? config.footSprite : defaultSprite;
        sr.color        = config.footColor;
        sr.sortingOrder = config.footSortingOrder;

        // Radius 0.5 local → footRadius world units (visual is scaled by diameter)
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.linearDamping = 4f;
        rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        // gravity enabled by default — feet fall naturally to the ground

        return rb;
    }

    // -------------------------------------------------------------------------
    // Editor test
    // -------------------------------------------------------------------------

    [Header("Test")]
    [SerializeField] private PlayerConfig testConfig;
    [SerializeField] private Vector2 testPosition;

    [ContextMenu("Test Spawn")]
    private void TestSpawn()
    {
        if (!Application.isPlaying || testConfig == null) return;
        Spawn(testConfig, testPosition);
    }
}
