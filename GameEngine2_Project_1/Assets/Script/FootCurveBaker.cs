using UnityEngine;
using UnityEditor;

public class FootCurveBaker : EditorWindow
{
    private AnimationClip clip;
    private HumanBodyBones footBone = HumanBodyBones.RightFoot;
    private Transform refRoot;
    private float sampleRate = 60f;
    private float contactNorm = 0.5f;

    [MenuItem("Tools/IK/Foot Curve Baker")]
    static void Init()
    {
        GetWindow<FootCurveBaker>("Foot Curve Baker");
    }

    void OnGUI()
    {
        clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", clip, typeof(AnimationClip), false);
        refRoot = (Transform)EditorGUILayout.ObjectField("Reference Root", refRoot, typeof(Transform), true);
        footBone = (HumanBodyBones)EditorGUILayout.EnumPopup("Foot Bone", footBone);
        sampleRate = EditorGUILayout.FloatField("Sample Rate", sampleRate);
        contactNorm = EditorGUILayout.Slider("Contact Norm", contactNorm, 0f, 1f);

        if (GUILayout.Button("Bake Curve"))
        {
            Bake();
        }
    }

    void Bake()
    {
        if (!clip || !refRoot) return;

        var animator = refRoot.GetComponent<Animator>();
        if (!animator || !animator.isHuman)
        {
            Debug.LogError("Animator(Humanoid) 필요");
            return;
        }

        var footTr = animator.GetBoneTransform(footBone);
        if (!footTr)
        {
            Debug.LogError("발 본을 찾을 수 없음");
            return;
        }

        var curveX = new AnimationCurve();
        var curveY = new AnimationCurve();
        var curveZ = new AnimationCurve();

        float length = clip.length;
        int steps = Mathf.CeilToInt(length * sampleRate);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps; // normalized
            float time = t * length;

            clip.SampleAnimation(refRoot.gameObject, time);

            Vector3 localPos = refRoot.InverseTransformPoint(footTr.position);

            curveX.AddKey(t, localPos.x);
            curveY.AddKey(t, localPos.y);
            curveZ.AddKey(t, localPos.z);
        }

        // contactNorm 시점 좌표 샘플링
        float contactTime = contactNorm * length;
        clip.SampleAnimation(refRoot.gameObject, contactTime);
        Vector3 contactLocalPos = refRoot.InverseTransformPoint(footTr.position);

        // ScriptableObject 생성
        FootCurveData data = ScriptableObject.CreateInstance<FootCurveData>();
        data.curveX = curveX;
        data.curveY = curveY;
        data.curveZ = curveZ;
        data.contactNorm = contactNorm;
        data.contactPosition = contactLocalPos; // 저장

        // 저장
        string path = "Assets/FootCurveData.asset";
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"Foot curve data saved to {path}, contactPos={contactLocalPos}");
    }
}
