using UnityEngine;

/// <summary>
/// Holds per-instance runtime state for BallEffect subclasses.
/// Added to the Ball GameObject at launch so each ball tracks its own trail
/// independently of the shared BallEffect ScriptableObject asset.
/// </summary>
public class BallState : MonoBehaviour
{
    public GameObject trail;
}