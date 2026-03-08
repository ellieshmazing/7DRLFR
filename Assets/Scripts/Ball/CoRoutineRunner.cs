using UnityEngine;

/// <summary>
/// Persistent singleton MonoBehaviour for running coroutines from contexts
/// that can't run them directly (e.g. ScriptableObjects, or objects about to be destroyed).
/// </summary>
public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;

    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("CoroutineRunner");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineRunner>();
            }
            return _instance;
        }
    }
}