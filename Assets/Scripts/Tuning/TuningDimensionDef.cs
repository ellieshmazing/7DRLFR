using UnityEngine;

[System.Serializable]
public struct TuningVariable
{
    [Tooltip("Config SO containing the target field (PlayerConfig or CentipedeConfig)")]
    public ScriptableObject targetConfig;

    [Tooltip("Reflection target field name, e.g. 'moveForce'")]
    public string fieldName;

    public float min;
    public float max;
    public float defaultValue;

    [Tooltip("True for rb.mass, collider setup, pathfinder params — triggers auto-respawn")]
    public bool requiresRespawn;

    [Tooltip("True if live MonoBehaviour instances need dual-write (e.g. NodeWiggle)")]
    public bool liveSync;
}

[CreateAssetMenu(fileName = "NewTuningDimension", menuName = "Tuning/Dimension")]
public class TuningDimensionDef : ScriptableObject
{
    [Tooltip("Display name shown in overlay, e.g. 'Foot Physics'")]
    public string dimensionName;

    [TextArea(2, 4)]
    [Tooltip("What to do while tuning this dimension")]
    public string testScenario;

    public TuningVariable[] variables;

    [Tooltip("Seconds for one min→max sine cycle during sweep phase")]
    [Min(1f)] public float sweepDuration = 10f;

    [Range(0.10f, 0.25f)]
    [Tooltip("Step size as a fraction of the variable range, applied after each respawn observation window (init-only / requiresRespawn variables only)")]
    public float respawnStepFraction = 0.15f;
}
