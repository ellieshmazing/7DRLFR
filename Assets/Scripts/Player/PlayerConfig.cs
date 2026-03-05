using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerConfig", menuName = "Player/Config")]
public class PlayerConfig : ScriptableObject
{
    [Header("Movement")]
    [Min(0.1f)] public float moveForce = 15f;
    [Min(0.1f)] public float maxSpeed = 5f;

    [Header("Stance")]
    [Tooltip("Vertical distance between torso and the lowest foot visual (ground level)")]
    [Min(0f)] public float standHeight = 0.6f;

    [Header("Foot Formation")]
    [Tooltip("Horizontal distance from torso center to each foot node")]
    [Min(0f)] public float footSpreadX = 0.2f;
    [Tooltip("Vertical offset of foot nodes below the torso node (negative = below)")]
    public float footOffsetY = -0.4f;
    [Tooltip("World-unit radius for foot visual and collider")]
    [Min(0.01f)] public float footRadius = 0.15f;

    [Header("Torso Visual")]
    [Tooltip("World-unit radius for torso sprite")]
    [Min(0.01f)] public float torsoRadius = 0.25f;

    [Header("Wiggle — Torso")]
    [Min(0f)] public float torsoWiggleStiffness = 80f;
    [Min(0f)] public float torsoWiggleDamping = 5f;
    [Min(0.01f)] public float torsoWiggleMass = 1f;

    [Header("Wiggle — Feet")]
    [Min(0f)] public float footWiggleStiffness = 60f;
    [Min(0f)] public float footWiggleDamping = 8f;
    [Min(0.01f)] public float footWiggleMass = 0.5f;

    [Header("Arms")]
    [Tooltip("Distance from torso visual center to the arm pivot")]
    [Min(0f)] public float armOrbitRadius = 0.3f;
    [Tooltip("Uniform scale applied to the arm sprite GameObject")]
    [Min(0.01f)] public float armScale = 0.25f;

    [Header("Sprites")]
    public Sprite torsoSprite;
    public Sprite footSprite;
    public Sprite armSprite;

    [Header("Colors")]
    public Color torsoColor = Color.white;
    public Color footColor = Color.white;
    public Color armColor = Color.white;

    [Header("Sorting Order")]
    public int footSortingOrder  = -1;
    public int torsoSortingOrder =  0;
    public int armSortingOrder   =  1;

    [Header("Debug Gizmos")]
    public Color torsoNodeGizmoColor = Color.cyan;
    public Color footNodeGizmoColor  = Color.yellow;
}
