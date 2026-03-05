using UnityEngine;

[RequireComponent(typeof(SkeletonNode))]
[RequireComponent(typeof(Rigidbody2D))]
public class SkeletonRoot : MonoBehaviour
{
    private SkeletonNode rootNode;

    void Awake()
    {
        rootNode = GetComponent<SkeletonNode>();
    }

    void Start()
    {
        BuildTreeFromHierarchy(rootNode);
        rootNode.InitializeTrail();
    }

    void BuildTreeFromHierarchy(SkeletonNode node)
    {
        node.children.Clear();

        foreach (Transform child in node.transform)
        {
            var childNode = child.GetComponent<SkeletonNode>();
            if (childNode != null)
            {
                childNode.parent = node;
                node.children.Add(childNode);
                BuildTreeFromHierarchy(childNode); // recurse
            }
        }
    }

    void FixedUpdate()
    {
        rootNode.RecordAndPropagate();
    }
}