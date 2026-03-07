using UnityEngine;

[CreateAssetMenu(fileName = "NewCameraConfig", menuName = "Camera/Config")]
public class CameraConfig : ScriptableObject
{
    [Header("Smoothing")]
    [Tooltip("How quickly the camera moves to the target position. Higher = snappier.")]
    [Min(0.01f)]
    public float smoothing = 5f;

    [Header("Offset")]
    [Tooltip("World-space positional offset from the target (e.g. 0, 3, -8 = behind and above).")]
    public Vector3 positionOffset = new Vector3(0f, 3f, -8f);

    [Tooltip("Camera rotation expressed as Euler angles in degrees (e.g. 10, 0, 0 = slight downward tilt).")]
    public Vector3 rotationOffset = new Vector3(10f, 0f, 0f);
}