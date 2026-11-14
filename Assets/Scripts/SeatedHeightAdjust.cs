using System.Collections;
using UnityEngine;

// 坐姿自适应：开场按头显实测眼高整体抬升 XR Origin，坐小凳子也能获得站立视角
// 站着玩（眼高 >= 判定阈值）则完全不动 rig
public class SeatedHeightAdjust : MonoBehaviour
{
    [Header("头低于该高度判定为坐姿（米）")]
    public float seatedEyeHeight = 1.15f;

    [Header("坐姿时把眼高抬升到（米）")]
    public float targetEyeHeight = 1.32f;

    [Header("开场延迟测量秒数（等追踪稳定）")]
    public float measureDelay = 1f;

    [Header("单次抬升上限（米），防追踪异常值")]
    public float maxLift = 1.2f;

    bool applied;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(measureDelay);
        TryApplyHeight();
    }

    // 按当前头显实测眼高执行一次抬升；返回实际抬升量（站姿返回 0）
    public float TryApplyHeight()
    {
        if (applied) return 0f;
        Camera head = GetComponentInChildren<Camera>();
        if (head == null) return 0f;
        float eyeY = head.transform.position.y;
        if (eyeY >= seatedEyeHeight) return 0f;
        float lift = Mathf.Clamp(targetEyeHeight - eyeY, 0f, maxLift);
        transform.position += Vector3.up * lift;
        applied = true;
        Debug.Log("[SeatedHeightAdjust] 坐姿检测: 眼高 " + eyeY.ToString("F2") + "m，rig 抬升 " + lift.ToString("F2") + "m");
        return lift;
    }
}
