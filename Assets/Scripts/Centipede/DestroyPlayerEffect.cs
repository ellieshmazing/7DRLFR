using UnityEngine;

/// <summary>
/// Default pincer hit effect: unregisters the player from PlayerRegistry
/// and destroys the player GameObject.
/// Replace or supplement with other IPlayerHitEffect implementations for
/// stun, knockback, damage, etc.
/// </summary>
public sealed class DestroyPlayerEffect : IPlayerHitEffect
{
    public void Apply(GameObject playerGO)
    {
        PlayerRegistry.Unregister(playerGO.transform);
        Object.Destroy(playerGO);
    }
}
