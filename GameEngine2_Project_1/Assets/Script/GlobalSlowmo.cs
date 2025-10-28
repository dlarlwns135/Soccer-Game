using UnityEngine;

public class GlobalSlowmo : MonoBehaviour
{
    [Range(0.01f, 1f)]
    public float slowScale = 0.1f;   // 슬로모 비율 (0.2 = 20%)
    private float originalFixedDelta;

    void Start()
    {
        // FixedUpdate 물리 보정을 위해 원래 값 저장
        originalFixedDelta = Time.fixedDeltaTime;
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.P))
        {
            // P키 누르는 동안 슬로모
            Time.timeScale = slowScale;
            Time.fixedDeltaTime = originalFixedDelta * slowScale;
        }
        else
        {
            // 평소 속도 복원
            Time.timeScale = 1f;
            Time.fixedDeltaTime = originalFixedDelta;
        }
    }
}
