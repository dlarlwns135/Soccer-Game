using UnityEngine;
using UnityEditor;
using System.IO;

public class HandCurveBaker : EditorWindow
{
    private AnimationClip clip;
    private Transform refRoot;
    private float sampleRate = 60f;
    private float contactNorm = 0.5f;

    [MenuItem("Tools/IK/Hand Curve Baker")]
    static void Init()
    {
        GetWindow<HandCurveBaker>("Hand Curve Baker");
    }

    void OnGUI()
    {
        clip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", clip, typeof(AnimationClip), false);
        refRoot = (Transform)EditorGUILayout.ObjectField("Reference Root", refRoot, typeof(Transform), true);
        sampleRate = EditorGUILayout.FloatField("Sample Rate", sampleRate);
        contactNorm = EditorGUILayout.Slider("Contact Norm", contactNorm, 0f, 1f);

        EditorGUILayout.Space();

        if (GUILayout.Button("Bake Left / Right Hand Curves"))
        {
            BakeHands();
        }
    }

    void BakeHands()
    {
        if (!clip || !refRoot)
        {
            Debug.LogError("AnimationClip과 Reference Root를 지정해야 합니다.");
            return;
        }

        var animator = refRoot.GetComponent<Animator>();
        if (!animator || !animator.isHuman)
        {
            Debug.LogError("Reference Root에 Humanoid Animator가 필요합니다.");
            return;
        }

        // 저장할 폴더 결정: 클립 있는 폴더를 기본으로 사용
        string clipPath = AssetDatabase.GetAssetPath(clip);
        string dir = string.IsNullOrEmpty(clipPath) ? "Assets" : Path.GetDirectoryName(clipPath);
        if (string.IsNullOrEmpty(dir)) dir = "Assets";

        BakeSingleHand(animator, HumanBodyBones.LeftHand, Path.Combine(dir, clip.name + "_LHandCurveData.asset"));
        BakeSingleHand(animator, HumanBodyBones.RightHand, Path.Combine(dir, clip.name + "_RHandCurveData.asset"));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    void BakeSingleHand(Animator animator, HumanBodyBones handBone, string assetPath)
    {
        var handTr = animator.GetBoneTransform(handBone);
        if (!handTr)
        {
            Debug.LogError($"{handBone} 본을 찾을 수 없습니다. 스킵합니다.");
            return;
        }

        var curveX = new AnimationCurve();
        var curveY = new AnimationCurve();
        var curveZ = new AnimationCurve();

        float length = clip.length;
        int steps = Mathf.CeilToInt(length * sampleRate);

        // 애니메이션 전체 구간을 0~1 정규화해서 샘플링
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps; // normalized 0~1
            float time = t * length;

            clip.SampleAnimation(refRoot.gameObject, time);

            // 루트 기준 로컬 좌표로 변환
            Vector3 localPos = refRoot.InverseTransformPoint(handTr.position);

            curveX.AddKey(t, localPos.x);
            curveY.AddKey(t, localPos.y);
            curveZ.AddKey(t, localPos.z);
        }

        // contactNorm 시점 좌표 샘플링
        float contactTime = contactNorm * length;
        clip.SampleAnimation(refRoot.gameObject, contactTime);
        Vector3 contactLocalPos = refRoot.InverseTransformPoint(handTr.position);

        // ScriptableObject 생성
        FootCurveData data = ScriptableObject.CreateInstance<FootCurveData>();
        data.curveX = curveX;
        data.curveY = curveY;
        data.curveZ = curveZ;
        data.contactNorm = contactNorm;
        data.contactPosition = contactLocalPos;

        // 기존 에셋이 있으면 덮어쓰기
        var existing = AssetDatabase.LoadAssetAtPath<FootCurveData>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(data, assetPath);
            Debug.Log($"[{handBone}] curve data created: {assetPath}, contactPos={contactLocalPos}");
        }
        else
        {
            EditorUtility.CopySerialized(data, existing);
            Debug.Log($"[{handBone}] curve data updated: {assetPath}, contactPos={contactLocalPos}");
        }
    }
}
