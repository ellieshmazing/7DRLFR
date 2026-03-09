using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles centipede splitting when balls are detached by external systems
/// (e.g. explosion radius from FireballEffect).
///
/// Placed on the same GameObject as SkeletonRoot. Created by CentipedeAssembler
/// on initial spawn, and by HandleDetachment on each split sub-centipede.
/// </summary>
[DefaultExecutionOrder(5)]
public class CentipedeController : MonoBehaviour
{
    private CentipedeConfig config;
    private List<SkeletonNode> nodes = new List<SkeletonNode>();
    private List<Ball> balls = new List<Ball>();

    /// <summary>Exposes the config for runtime queries (e.g. AutoRespawner filtering).</summary>
    public CentipedeConfig Config => config;

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by CentipedeAssembler after spawning.
    /// SkeletonRoot (already on this GO) owns trail recording and propagation.
    /// </summary>
    public void Initialize(CentipedeConfig cfg, List<SkeletonNode> nodeList, List<Ball> ballList)
    {
        config = cfg;
        nodes  = new List<SkeletonNode>(nodeList);
        balls  = new List<Ball>(ballList);
    }

    /// <summary>
    /// Called internally for split sub-centipede heads.
    /// A new SkeletonRoot is added to this GO separately; this method only
    /// registers the segment lists (trail is seeded by SkeletonRoot.Initialize).
    /// </summary>
    private void InitializeAsSplit(CentipedeConfig cfg, List<SkeletonNode> nodeList, List<Ball> ballList)
    {
        config = cfg;
        nodes  = new List<SkeletonNode>(nodeList);
        balls  = new List<Ball>(ballList);
    }

    // ── Public detachment API ─────────────────────────────────────────────────

    /// <summary>
    /// Detaches the given balls from this centipede and runs chain resolution
    /// (splitting, freeing solo balls, spawning reversed sub-centipedes).
    /// Called by external systems such as FireballEffect on explosion.
    /// </summary>
    public void DetachBalls(HashSet<Ball> affectedBalls)
    {
        bool[] detachMask = new bool[balls.Count];
        bool any = false;
        for (int i = 0; i < balls.Count; i++)
        {
            if (balls[i] != null && affectedBalls.Contains(balls[i]))
            {
                detachMask[i] = true;
                any = true;
            }
        }

        if (!any) return;

        HandleDetachment(detachMask);
    }

    // ── Core destruction logic ────────────────────────────────────────────────

    private void HandleDetachment(bool[] detachMask)
    {
        // ── Step 1: Find chains of consecutive non-detached indices ───────────
        var chains = new List<(int start, int end)>();
        int chainStart = -1;
        for (int i = 0; i <= nodes.Count; i++)
        {
            bool det = i == nodes.Count || detachMask[i];
            if (!det && chainStart < 0)              chainStart = i;
            else if (det && chainStart >= 0)  { chains.Add((chainStart, i - 1)); chainStart = -1; }
        }

        // ── Step 2: Reparent non-zero chain heads before any destruction ──────
        //
        // The first node of each non-zero chain is a Unity child of a detached node
        // (or was before this frame). Moving it to scene root brings its entire
        // Unity subtree along, preventing destruction when the detached parent is destroyed.
        foreach (var (s, _) in chains)
        {
            if (s > 0)
                nodes[s].transform.SetParent(null, true);
        }

        // ── Step 3: Unparent balls that must survive their node GO's destruction ──
        for (int i = 0; i < detachMask.Length; i++)
        {
            if (detachMask[i])
                balls[i].transform.SetParent(null, true);
        }

        foreach (var (s, e) in chains)
        {
            if (e == s)   // solo non-detached node → will become a free ball
                balls[s].transform.SetParent(null, true);
        }

        // ── Step 4: Destroy detached node GOs ────────────────────────────────
        //
        // For consecutive detached runs (e.g. {3,4}), only the topmost one in
        // the Unity hierarchy (lowest index) is destroyed explicitly; the rest
        // are already children of it and will be destroyed along with it.
        for (int i = 0; i < detachMask.Length; i++)
        {
            if (detachMask[i] && (i == 0 || !detachMask[i - 1]))
                Destroy(nodes[i].gameObject);
        }

        // ── Step 5: Free detached balls ───────────────────────────────────────
        for (int i = 0; i < detachMask.Length; i++)
        {
            if (detachMask[i] && balls[i] != null)
                balls[i].Detach(balls[i].SpringVelocity);
        }

        // ── Step 6: Resolve each surviving chain ──────────────────────────────
        bool zeroChainSurvivesAsCentipede = false;

        foreach (var (s, e) in chains)
        {
            int length = e - s + 1;

            if (s == 0)
            {
                // Original-direction chain — SkeletonRoot (already on this GO) keeps driving it.
                if (length >= 2)
                {
                    nodes[e].children.Clear();         // sever SkeletonNode link to detached tail
                    nodes = nodes.GetRange(0, length);
                    balls = balls.GetRange(0, length);
                    zeroChainSurvivesAsCentipede = true;
                }
                else
                {
                    // Single node left at root position — becomes a free ball.
                    nodes[0].children.Clear();
                    nodes[0].parent = null;
                    balls[0].Detach(balls[0].SpringVelocity);
                    // Ball was unparented in step 3; this GO (which has SkeletonRoot +
                    // CentipedeController) is destroyed in step 7.
                }
            }
            else
            {
                if (length == 1)
                {
                    // Solo surviving node — free ball.
                    SkeletonNode n = nodes[s];
                    Ball         b = balls[s];
                    b.Detach(b.SpringVelocity);
                    n.children.Clear();
                    n.parent = null;
                    Destroy(n.gameObject);
                }
                else
                {
                    SpawnSplitCentipede(s, e);
                }
            }
        }

        // ── Step 7: Destroy this controller if the original chain is gone ─────
        if (!zeroChainSurvivesAsCentipede)
            Destroy(gameObject);
    }

    /// <summary>
    /// Creates a new reversed sub-centipede from the node range [s, e].
    ///
    /// "Reversed" because the node closest to the original tail becomes the new head:
    /// it was already moving in a defined direction and can continue autonomously.
    /// The SkeletonNode parent/children refs and the Unity transform hierarchy are
    /// both rebuilt in the new order before the new SkeletonRoot is initialized.
    /// </summary>
    private void SpawnSplitCentipede(int s, int e)
    {
        // Collect and reverse so index 0 = new head (was original tail of this chain).
        var chainNodes = new List<SkeletonNode>();
        var chainBalls = new List<Ball>();
        for (int i = s; i <= e; i++) { chainNodes.Add(nodes[i]); chainBalls.Add(balls[i]); }
        chainNodes.Reverse();
        chainBalls.Reverse();

        // Rebuild SkeletonNode logical tree in reversed order.
        for (int i = 0; i < chainNodes.Count; i++)
        {
            chainNodes[i].parent = i > 0 ? chainNodes[i - 1] : null;
            chainNodes[i].children.Clear();
            if (i > 0) chainNodes[i - 1].children.Add(chainNodes[i]);
        }

        // Rebuild Unity hierarchy in reversed order.
        // After step 3, chainNodes[chainNodes.Count-1] (original first of chain, = nodes[s])
        // is at scene root with the rest nested beneath it.
        // Unparent all to scene root from lowest original index to highest,
        // then re-parent in new head→tail order.
        for (int i = chainNodes.Count - 1; i >= 0; i--)
            chainNodes[i].transform.SetParent(null, true);

        for (int i = 1; i < chainNodes.Count; i++)
            chainNodes[i].transform.SetParent(chainNodes[i - 1].transform, true);

        // Add SkeletonRoot to new head. Initialize() seeds the trail and suppresses
        // the automatic Start() build (which reads Unity hierarchy, not needed here).
        GameObject newHeadGO = chainNodes[0].gameObject;
        var newRoot = newHeadGO.AddComponent<SkeletonRoot>();
        newRoot.Initialize(chainNodes[0]);

        // Add CentipedeController to new head.
        var newController = newHeadGO.AddComponent<CentipedeController>();
        newController.InitializeAsSplit(config, chainNodes, chainBalls);

        // Mirror DebugMouseFollow from the original head, if present.
        // [RequireComponent(Rigidbody2D)] on DebugMouseFollow auto-adds an RB to the new head.
        var origMouseFollow = GetComponent<DebugMouseFollow>();
        if (origMouseFollow != null)
        {
            var newMouseFollow = newHeadGO.AddComponent<DebugMouseFollow>();
            newMouseFollow.speed    = origMouseFollow.speed;
            newMouseFollow.deadZone = origMouseFollow.deadZone;
        }

        // Mirror ScentFieldNavigator from the original head, if present.
        var origNavigator = GetComponent<ScentFieldNavigator>();
        if (origNavigator != null)
        {
            var newNavigator = newHeadGO.AddComponent<ScentFieldNavigator>();
            newNavigator.Initialize(config, origNavigator.Target, ScentField.GetOrCreate());
        }
    }
}
