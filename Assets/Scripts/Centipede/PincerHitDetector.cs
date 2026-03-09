using UnityEngine;

/// <summary>
/// Attached to each pincer hitbox GO. Detects player contact via trigger
/// and delegates to PincerController for effect dispatch.
/// The hitbox GOs are children of the head (not the animated sprite),
/// so this collider never rotates with the pincer animation.
/// </summary>
public class PincerHitDetector : MonoBehaviour
{
    [HideInInspector] public PincerController controller;

    void OnTriggerEnter2D(Collider2D other)
    {
        Transform root = other.transform.root;
        if (root == PlayerRegistry.PlayerTransform)
            controller.HandlePlayerHit(root.gameObject);
    }
}
