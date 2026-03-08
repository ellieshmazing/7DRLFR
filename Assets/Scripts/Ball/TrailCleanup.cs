using UnityEngine;

/// <summary>
/// Utility component added at runtime to particle trail GameObjects.
/// Stops all child particle systems from emitting and destroys the GameObject
/// after a linger duration so existing particles can fade out naturally.
/// </summary>
public class TrailCleanup : MonoBehaviour
{
    public void StopAndDestroy(float lingerDuration)
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(gameObject, lingerDuration);
    }
}