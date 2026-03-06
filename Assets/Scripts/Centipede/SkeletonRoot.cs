using UnityEngine;

/// <summary>
/// Drives the centipede skeleton: records the root node's trail and propagates
/// positions to every child node each FixedUpdate.
///
/// Can be initialized from Unity Start (initial spawn) or via Initialize()
/// (runtime split — called immediately after AddComponent, before Start fires).
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(SkeletonNode))]
public class SkeletonRoot : MonoBehaviour
{
    private SkeletonNode rootNode;
    private bool initialized;

    void Awake()
    {
        rootNode = GetComponent<SkeletonNode>();
    }

    void Start()
    {
        if (initialized) return;

        BuildTreeFromHierarchy(rootNode);
        rootNode.InitializeTrail();
        initialized = true;
    }

    /// <summary>
    /// Runtime initialization for centipedes created by splitting.
    /// Skips BuildTreeFromHierarchy — SkeletonNode parent/children refs must already be correct.
    /// </summary>
    public void Initialize(SkeletonNode root)
    {
        rootNode = root;
        rootNode.InitializeTrail();
        initialized = true;
    }

    void FixedUpdate()
    {
        rootNode.RecordAndPropagate();
    }

    private void BuildTreeFromHierarchy(SkeletonNode node)
    {
        node.children.Clear();

        foreach (Transform child in node.transform)
        {
            var childNode = child.GetComponent<SkeletonNode>();
            if (childNode == null) continue;

            childNode.parent = node;
            node.children.Add(childNode);
            BuildTreeFromHierarchy(childNode);
        }
    }
}
