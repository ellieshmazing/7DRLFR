using UnityEngine;

/// <summary>
/// Strategy interface for effects applied to the player on pincer contact.
/// Implement this and register with PincerController to add new hit behaviors
/// without modifying the controller.
/// </summary>
public interface IPlayerHitEffect
{
    void Apply(GameObject playerGO);
}
