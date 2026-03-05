using UnityEngine;
using System.Collections.Generic;

public class SkeletonNode : MonoBehaviour
{
    [Header("Tree Structure")]
    public SkeletonNode parent;
    public List<SkeletonNode> children = new List<SkeletonNode>();

    [Header("Follow Settings")]
    [Tooltip("How far behind its parent this node follows along the path")]
    public float followDistance = 0.3f;

    public Color gizmoColor = Color.green;

    /// <summary>
    /// World-space position this node occupies, updated each FixedUpdate.
    /// </summary>
    public Vector2 TargetWorldPosition { get; private set; }

    private struct TrailPoint
    {
        public Vector2 position;
        public float distance;
    }

    private readonly List<TrailPoint> trail = new List<TrailPoint>();
    private float totalDistance;

    /// <summary>
    /// Seeds this node's trail so children start at their current positions.
    /// Call once after the skeleton tree is built.
    /// </summary>
    public void InitializeTrail()
    {
        trail.Clear();
        Vector2 currentPos = (Vector2)transform.position;
        TargetWorldPosition = currentPos;

        float requiredLength = GetMaxChildFollowDistance();

        if (requiredLength > 0f && children.Count > 0)
        {
            // Determine forward direction from first child toward this node
            Vector2 childPos = (Vector2)children[0].transform.position;
            Vector2 toHere = currentPos - childPos;
            float mag = toHere.magnitude;
            Vector2 forward = mag > 0.0001f ? toHere / mag : Vector2.up;

            // Seed a straight-line trail behind the current position
            Vector2 startPos = currentPos - forward * requiredLength;
            totalDistance = requiredLength;
            trail.Add(new TrailPoint { position = startPos, distance = 0f });
            trail.Add(new TrailPoint { position = currentPos, distance = requiredLength });
        }
        else
        {
            totalDistance = 0f;
            trail.Add(new TrailPoint { position = currentPos, distance = 0f });
        }

        foreach (var child in children)
            child.InitializeTrail();
    }

    /// <summary>
    /// Records this node's current position to its trail,
    /// then moves all children along the trail path.
    /// </summary>
    public void RecordAndPropagate()
    {
        Vector2 currentPos = (Vector2)transform.position;

        if (trail.Count > 0)
        {
            float delta = Vector2.Distance(currentPos, trail[trail.Count - 1].position);
            if (delta > 0.0001f)
            {
                totalDistance += delta;
                trail.Add(new TrailPoint { position = currentPos, distance = totalDistance });
            }
        }
        else
        {
            trail.Add(new TrailPoint { position = currentPos, distance = totalDistance });
        }

        TargetWorldPosition = currentPos;

        foreach (var child in children)
        {
            child.FollowParentTrail();
            child.RecordAndPropagate();
        }

        PruneTrail();
    }

    /// <summary>
    /// Positions this node along its parent's trail at followDistance behind.
    /// Uses binary search and linear interpolation for accuracy regardless of speed.
    /// </summary>
    private void FollowParentTrail()
    {
        if (parent == null) return;

        var parentTrail = parent.trail;
        if (parentTrail.Count == 0) return;

        float targetDist = parent.totalDistance - followDistance;

        // Clamp to trail bounds
        if (targetDist <= parentTrail[0].distance)
        {
            transform.position = (Vector3)parentTrail[0].position;
            return;
        }

        if (targetDist >= parentTrail[parentTrail.Count - 1].distance)
        {
            transform.position = (Vector3)parentTrail[parentTrail.Count - 1].position;
            return;
        }

        // Binary search for the segment containing targetDist
        int lo = 0, hi = parentTrail.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (parentTrail[mid].distance <= targetDist)
                lo = mid;
            else
                hi = mid;
        }

        // Interpolate within the segment
        float segLen = parentTrail[hi].distance - parentTrail[lo].distance;
        float t = segLen > 0.0001f
            ? (targetDist - parentTrail[lo].distance) / segLen
            : 0f;

        Vector2 pos = Vector2.Lerp(parentTrail[lo].position, parentTrail[hi].position, t);
        transform.position = (Vector3)pos;
    }

    /// <summary>
    /// Removes trail points that are too old for any child to need.
    /// </summary>
    private void PruneTrail()
    {
        if (children.Count == 0 || trail.Count <= 2) return;

        float maxFollow = GetMaxChildFollowDistance();
        float minNeeded = totalDistance - maxFollow;

        // Keep one point before minNeeded for interpolation
        int removeCount = 0;
        for (int i = 0; i < trail.Count - 2; i++)
        {
            if (trail[i + 1].distance < minNeeded)
                removeCount++;
            else
                break;
        }

        if (removeCount > 0)
            trail.RemoveRange(0, removeCount);
    }

    private float GetMaxChildFollowDistance()
    {
        float max = 0f;
        foreach (var child in children)
            max = Mathf.Max(max, child.followDistance);
        return max;
    }

    /// <summary>
    /// Dynamically add a child node (for runtime limb addition).
    /// </summary>
    public void AddChild(SkeletonNode child)
    {
        child.parent = this;
        children.Add(child);
    }

    public void RemoveChild(SkeletonNode child)
    {
        child.parent = null;
        children.Remove(child);
    }

    /// <summary>
    /// Visualizes skeleton wireframe for debugging purposes.
    /// </summary>
    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        if (parent != null)
        {
            Gizmos.DrawLine(transform.position,
                parent.transform.position);
        }
        Gizmos.DrawWireSphere(transform.position, 0.05f);
    }

    /// <summary>
    /// Enforces followDistance from parent while editing.
    /// </summary>
    void OnValidate()
    {
        if (!Application.isPlaying && transform.parent != null)
        {
            var parentNode = transform.parent.GetComponent<SkeletonNode>();
            if (parentNode != null)
            {
                Vector2 parentPos = (Vector2)transform.parent.position;
                Vector2 currentPos = (Vector2)transform.position;
                Vector2 dir = currentPos - parentPos;
                float mag = dir.magnitude;

                if (mag > 0.0001f)
                    transform.position = (Vector3)(parentPos + (dir / mag) * followDistance);
                else
                    transform.position = (Vector3)(parentPos + Vector2.down * followDistance);
            }
        }
    }
}
