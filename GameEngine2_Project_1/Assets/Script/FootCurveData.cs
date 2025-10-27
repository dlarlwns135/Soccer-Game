using UnityEngine;

[CreateAssetMenu(fileName = "FootCurveData", menuName = "IK/Foot Curve Data")]
public class FootCurveData : ScriptableObject
{
    public AnimationCurve curveX;
    public AnimationCurve curveY;
    public AnimationCurve curveZ;

    [Range(0f, 1f)]
    public float contactNorm;  // ¡¢√À Ω√¡°(normalizedTime)
    public Vector3 contactPosition;
}
