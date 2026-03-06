using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Monitors centipede Balls for displacement past config.detachDistance and handles
/// splitting the centipede when detachment occurs.
///
/// Runs at execution order 5 — after SkeletonRoot (-10, trail update) and
/// Ball (0, spring chase) — so displacement is measured against this frame's
/// node positions with the ball's physics position from the last step.
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

    // ── Per-frame ─────────────────────────────────────────────────────────────

    void FixedUpdate()
    {
        if (config == null || nodes.Count == 0) return;
        CheckForDetachment();
    }

    // ── Detachment detection ──────────────────────────────────────────────────

    private void CheckForDetachment()
    {
        List<int> triggered = null;
        for (int i = 0; i < balls.Count; i++)
        {
            if (balls[i] == null || nodes[i] == null) continue;
            float d = Vector2.Distance(
                (Vector2)balls[i].transform.position,
                (Vector2)nodes[i].transform.position);
            if (d >= config.detachDistance)
            {
                triggered ??= new List<int>();
                triggered.Add(i);
            }
        }

        if (triggered != null)
            HandleDetachment(triggered);
    }

    // ── Core destruction logic ────────────────────────────────────────────────

    private void HandleDetachment(List<int> initialDetached)
    {
        float D = config.detachDistance;

        // ── Step 1: Expand detached set to include preemptive detachments ─────
        //
        // A ball that hasn't yet crossed detachDistance may already have enough
        // kinetic + spring potential energy to do so (SHM approximation, no damping):
        //   mass·v² ≥ stiffness·(D²−d²)
        //
        // Identifying these now avoids a cascade of sequential HandleDetachment
        // calls in subsequent frames.
        var detachedSet = new HashSet<int>(initialDetached);
        for (int i = 0; i < balls.Count; i++)
        {
            if (detachedSet.Contains(i)) continue;
            Ball b = balls[i]; SkeletonNode n = nodes[i];
            if (b == null || n == null) continue;

            float d        = Vector2.Distance((Vector2)b.transform.position, (Vector2)n.transform.position);
            float speedSq  = b.SpringVelocity.sqrMagnitude;
            float neededSq = (b.springStiffness / b.springMass) * (D * D - d * d);

            // neededSq ≤ 0 → already at or past D (shouldn't happen — would have been triggered)
            if (neededSq <= 0f || speedSq >= neededSq)
                detachedSet.Add(i);
        }

        // ── Step 2: Find chains of consecutive non-detached indices ───────────
        var chains = new List<(int start, int end)>();
        int chainStart = -1;
        for (int i = 0; i <= nodes.Count; i++)
        {
            bool det = i == nodes.Count || detachedSet.Contains(i);
            if (!det && chainStart < 0)              chainStart = i;
            else if (det && chainStart >= 0)  { chains.Add((chainStart, i - 1)); chainStart = -1; }
        }

        // ── Step 3: Reparent non-zero chain heads before any destruction ──────
        //
        // The first node of each non-zero chain is a Unity child of a detached node
        // (or was before this frame). Moving it to scene root brings its entire
        // Unity subtree along, preventing destruction when the detached parent is destroyed.
        foreach (var (s, _) in chains)
        {
            if (s > 0)
                nodes[s].transform.SetParent(null, true);
        }

        // ── Step 4: Unparent balls that must survive their node GO's destruction ──
        foreach (int i in detachedSet)
            balls[i].transform.SetParent(null, true);

        foreach (var (s, e) in chains)
        {
            if (e == s)   // solo non-detached node → will become a free ball
                balls[s].transform.SetParent(null, true);
        }

        // ── Step 5: Destroy detached node GOs ────────────────────────────────
        //
        // For consecutive detached runs (e.g. {3,4}), only the topmost one in
        // the Unity hierarchy (lowest index) is destroyed explicitly; the rest
        // are already children of it and will be destroyed along with it.
        // (This prevents destroying an already-destroyed child object.)
        foreach (int i in detachedSet)
        {
            if (i == 0 || !detachedSet.Contains(i - 1))
                Destroy(nodes[i].gameObject);
        }

        // ── Step 6: Free detached balls ───────────────────────────────────────
        foreach (int i in detachedSet)
        {
            if (balls[i] != null)
                balls[i].Detach(balls[i].SpringVelocity);
        }

        // ── Step 7: Resolve each surviving chain ──────────────────────────────
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
                    // Ball was unparented in step 4; this GO (which has SkeletonRoot +
                    // CentipedeController) is destroyed in step 8.
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

        // ── Step 8: Destroy this controller if the original chain is gone ─────
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
    }
}
