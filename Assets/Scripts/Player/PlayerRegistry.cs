/// <summary>
/// Singleton-free registry for the active player transform.
/// PlayerAssembler registers on spawn; PlayerSkeletonRoot unregisters on destroy.
/// Any system that needs to track the player subscribes to OnPlayerChanged.
/// </summary>
public static class PlayerRegistry
{
    /// <summary>The current player's root transform, or null if no player exists.</summary>
    public static UnityEngine.Transform PlayerTransform { get; private set; }

    /// <summary>Fired whenever the tracked player changes (including when it becomes null).</summary>
    public static event System.Action<UnityEngine.Transform> OnPlayerChanged;

    public static void Register(UnityEngine.Transform t)
    {
        PlayerTransform = t;
        OnPlayerChanged?.Invoke(t);
    }

    public static void Unregister(UnityEngine.Transform t)
    {
        if (PlayerTransform != t) return;
        PlayerTransform = null;
        OnPlayerChanged?.Invoke(null);
    }
}
