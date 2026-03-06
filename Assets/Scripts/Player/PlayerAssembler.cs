using UnityEngine;

/// <summary>
/// Instantiates all GameObjects and components that make up the player
/// character at runtime, mirroring the pattern of CentipedeAssembler.
///
/// Hierarchy created by Spawn():
///
///   Player  (PlayerSkeletonNode [root], PlayerSkeletonRoot, Rigidbody2D [gravityScale=0])
///   ├── TorsoVisual      (NodeWiggle, TorsoLayerController)
///   │   ├── TorsoLayer_0  (SpriteRenderer)
///   │   └── ...           (one child per TorsoLayerDef in PlayerConfig)
///   ├── Arm              (PlayerArmController, ArmLayerController)
///   │   ├── ArmLayer_0    (SpriteRenderer)
///   │   └── ...           (one child per ArmLayerDef in PlayerConfig)
///   ├── HipNode          (PlayerSkeletonNode, PlayerHipNode, PlayerFeet)
///   ├── LeftFootVisual   (SpriteRenderer, CircleCollider2D, Rigidbody2D [Dynamic, gravity])
///   └── RightFootVisual  (SpriteRenderer, CircleCollider2D, Rigidbody2D [Dynamic, gravity])
///
/// All offsets in PlayerConfig are in source pixels (16px grid).
/// The assembler converts them to world units via:
///   pixelToWorld = playerScale / SPRITE_PX
/// where playerScale is the world-unit diameter of one 16px sprite.
/// </summary>
public class PlayerAssembler : MonoBehaviour
{
    [Header("Fallback Sprite")]
    [Tooltip("Circle sprite used when a config slot has no sprite assigned " +
             "(should be 1 world unit diameter at scale 1)")]
    public Sprite defaultSprite;

    [Header("Jump")]
    [Tooltip("Base jump impulse (kg·m/s). Actual velocity = jumpSpeed / footMass — " +
             "increase footMass without raising this and the player jumps lower.")]
    public float jumpSpeed = 8f;

    [Tooltip("Extra impulse per world-unit of hip-below-foot offset (kg·m/s per m). " +
             "Tune alongside footMass and hipWiggleStiffness/Mass. Adjustable while live.")]
    public float jumpOffsetFactor = 10f;

    // Source sprite width assumed for all sprites in this project.
    const float SPRITE_PX = 16f;
    // Pixels-per-unit set on the spritesheet import settings.
    const float PPU = 128f;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Spawns a fully assembled player at <paramref name="position"/>.
    /// Returns the root GameObject.
    /// </summary>
    public GameObject Spawn(PlayerConfig config, Vector2 position)
    {
        // --- Scale conversion ---
        // pixelToWorld: multiplier to convert a source-pixel distance to world units.
        // spriteLocalScale: localScale value that makes a 16px sprite appear playerScale wu wide.
        // colRadius: CircleCollider2D.radius in local space so the collider matches the sprite circle.
        if (config.playerScale < 0.001f)
            Debug.LogWarning("[PlayerAssembler] PlayerConfig.playerScale is ~0 — " +
                             "all offsets and sprites will collapse to zero size. " +
                             "Set playerScale to a positive value (e.g. 0.5) in the config asset.", this);

        float pixelToWorld    = config.playerScale / SPRITE_PX;
        float spriteLocalScale = config.playerScale * PPU / SPRITE_PX;
        float colRadius       = 0.5f * SPRITE_PX / PPU; // = 0.0625; constant regardless of playerScale

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
        playerRoot.moveForce       = config.moveForce;
        playerRoot.maxSpeed        = config.maxSpeed;
        playerRoot.standHeight     = config.standHeight * pixelToWorld;
        playerRoot.jumpSpeed       = jumpSpeed;
        playerRoot.jumpOffsetFactor = jumpOffsetFactor;

        // --- Torso visual (wiggles around torso node) ---
        // TorsoVisual is a plain parent with NodeWiggle; no SpriteRenderer of its own.
        // Each TorsoLayerDef spawns a child GO that is a purely visual sprite.
        var torsoVisualGO = new GameObject("TorsoVisual");
        torsoVisualGO.transform.SetParent(root.transform, false);
        torsoVisualGO.transform.localPosition = Vector3.zero;
        torsoVisualGO.transform.localScale    = Vector3.one;

        var torsoWiggle = torsoVisualGO.AddComponent<NodeWiggle>();
        torsoWiggle.stiffness = config.torsoWiggleStiffness;
        torsoWiggle.damping   = config.torsoWiggleDamping;
        torsoWiggle.mass      = config.torsoWiggleMass;

        var torsoLayerCtrl = torsoVisualGO.AddComponent<TorsoLayerController>();

        var torsoLayerDefs       = config.torsoLayers ?? System.Array.Empty<TorsoLayerDef>();
        var torsoLayerTransforms = new Transform[torsoLayerDefs.Length];
        for (int i = 0; i < torsoLayerDefs.Length; i++)
        {
            var def     = torsoLayerDefs[i];
            var layerGO = new GameObject($"TorsoLayer_{i}");
            layerGO.transform.SetParent(torsoVisualGO.transform, false);
            layerGO.transform.localPosition = (Vector3)(def.localOffset * pixelToWorld);
            layerGO.transform.localScale    = Vector3.one * spriteLocalScale;

            var sr = layerGO.AddComponent<SpriteRenderer>();
            sr.sprite       = def.sprite != null ? def.sprite : defaultSprite;
            sr.color        = def.color;
            sr.sortingOrder = def.sortingOrder;

            torsoLayerTransforms[i] = layerGO.transform;
        }
        torsoLayerCtrl.layerTransforms = torsoLayerTransforms;
        torsoLayerCtrl.config          = config;

        // --- Arm (orbits torso visual, points at mouse) ---
        // Arm is a plain pivot GO with the orbit controller; sprite layers are children.
        var armGO = new GameObject("Arm");
        armGO.transform.SetParent(root.transform, false);
        armGO.transform.localPosition = Vector3.zero;
        armGO.transform.localScale    = Vector3.one;

        var armCtrl = armGO.AddComponent<PlayerArmController>();
        armCtrl.torsoVisual = torsoVisualGO.transform;
        armCtrl.orbitRadius = config.armOrbitRadius * pixelToWorld;

        var armLayerCtrl = armGO.AddComponent<ArmLayerController>();

        var armLayerDefs       = config.armLayers ?? System.Array.Empty<ArmLayerDef>();
        var armLayerTransforms = new Transform[armLayerDefs.Length];
        for (int i = 0; i < armLayerDefs.Length; i++)
        {
            var def     = armLayerDefs[i];
            var layerGO = new GameObject($"ArmLayer_{i}");
            layerGO.transform.SetParent(armGO.transform, false);
            layerGO.transform.localPosition = (Vector3)(def.localOffset * pixelToWorld);
            layerGO.transform.localScale    = Vector3.one * spriteLocalScale;

            var sr = layerGO.AddComponent<SpriteRenderer>();
            sr.sprite       = def.sprite != null ? def.sprite : defaultSprite;
            sr.color        = def.color;
            sr.sortingOrder = def.sortingOrder;

            armLayerTransforms[i] = layerGO.transform;
        }
        armLayerCtrl.layerTransforms = armLayerTransforms;
        armLayerCtrl.config          = config;

        // --- Firing point and gun ---
        var firingPointGO = new GameObject("FiringPoint");
        firingPointGO.transform.SetParent(armGO.transform, false);
        firingPointGO.transform.localPosition = new Vector3(config.firingPointOffset * pixelToWorld, 0f, 0f);

        var gun = armGO.AddComponent<ProjectileGun>();
        gun.firingPoint     = firingPointGO.transform;
        gun.projectileDef   = config.projectileDef;
        gun.initialScale    = config.projectileInitialScale;
        gun.growTime        = config.projectileGrowTime;
        gun.firingSpeed     = config.firingSpeed;
        gun.fireCooldown    = config.fireCooldown;
        gun.tempMinDiameter = config.tempMinProjectileDiameter;
        gun.tempMaxDiameter = config.tempMaxProjectileDiameter;

        // --- Hip node ---
        // footOffsetY places it below the torso at spawn (negative value expected).
        // After the first FixedUpdate PlayerHipNode takes over Y from foot positions.
        float footOffsetWorldY = config.footOffsetY * pixelToWorld;
        Vector2 hipSpawnPos = new Vector2(position.x, position.y + footOffsetWorldY);

        var hipGO = new GameObject("HipNode");
        hipGO.transform.SetParent(root.transform, worldPositionStays: false);
        hipGO.transform.position = (Vector3)hipSpawnPos;

        var hipSkelNode = hipGO.AddComponent<PlayerSkeletonNode>();
        hipSkelNode.gizmoColor  = config.footNodeGizmoColor;
        hipSkelNode.localOffset = new Vector2(0f, footOffsetWorldY);

        var hipNodeScript = hipGO.AddComponent<PlayerHipNode>();
        hipNodeScript.stiffness = config.hipWiggleStiffness;
        hipNodeScript.damping   = config.hipWiggleDamping;
        hipNodeScript.mass      = config.hipWiggleMass;

        var feetScript       = hipGO.AddComponent<PlayerFeet>();
        feetScript.footSpreadX = config.footSpreadX * pixelToWorld;
        feetScript.stiffness   = config.footWiggleStiffness;
        feetScript.damping     = config.footWiggleDamping;
        feetScript.mass        = config.footWiggleMass;

        // Wire hip node into PlayerSkeletonRoot
        playerRoot.hipNode = hipGO.transform;

        // --- Foot visuals (siblings of HipNode, direct children of root) ---
        float footSpreadWorldX = config.footSpreadX * pixelToWorld;
        float footSpawnY       = hipSpawnPos.y;

        var leftFootRB  = CreateFootVisual("LeftFootVisual",  root.transform,
                              new Vector2(position.x - footSpreadWorldX, footSpawnY),
                              config.leftFoot, spriteLocalScale, colRadius, config);
        var rightFootRB = CreateFootVisual("RightFootVisual", root.transform,
                              new Vector2(position.x + footSpreadWorldX, footSpawnY),
                              config.rightFoot, spriteLocalScale, colRadius, config);

        // Wire foot RBs into both hip-node scripts
        hipNodeScript.leftFootRB  = leftFootRB;
        hipNodeScript.rightFootRB = rightFootRB;
        feetScript.leftFootRB     = leftFootRB;
        feetScript.rightFootRB    = rightFootRB;

        // Wire hip script and foot contacts into the skeleton root for jump
        playerRoot.hipNodeScript   = hipNodeScript;
        playerRoot.leftFootRB      = leftFootRB;
        playerRoot.rightFootRB     = rightFootRB;
        playerRoot.leftFootContact = leftFootRB.GetComponent<FootContact>();
        playerRoot.rightFootContact = rightFootRB.GetComponent<FootContact>();

        // Activating triggers Awake then Start on the complete hierarchy
        root.SetActive(true);

        return root;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private Rigidbody2D CreateFootVisual(
        string goName, Transform parent,
        Vector2 worldPos, FootDef footDef,
        float spriteLocalScale, float colRadius,
        PlayerConfig config)
    {
        var go = new GameObject(goName);
        go.layer = LayerMask.NameToLayer("Player");
        go.transform.SetParent(parent, worldPositionStays: true);
        go.transform.position   = (Vector3)worldPos;
        go.transform.localScale = Vector3.one * spriteLocalScale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = footDef.sprite != null ? footDef.sprite : defaultSprite;
        sr.color        = footDef.color;
        sr.sortingOrder = footDef.sortingOrder;

        // colRadius in local space → (playerScale / 2) world units after scaling,
        // matching the visual circle of the 16px sprite.
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = colRadius;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass          = config.footMass;
        rb.gravityScale  = config.footGravityScale;
        rb.linearDamping = 4f;
        rb.constraints   = RigidbodyConstraints2D.FreezeRotation;

        go.AddComponent<FootContact>();

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
