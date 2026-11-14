using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// 苍蝇拍拍面：仿 Assets/Scripts/HammerHit.cs 的挥速判定（≥minSwingSpeed 才算有效挥击，
/// 防止贴着土堆蹭分）。命中 FarmPest 交给其状态机结算；碰到虫情锣 = 开始新一轮。
/// 挂在拍面 trigger 碰撞体上；拍根需 Rigidbody（与 Grabbable 配套）。
/// </summary>
public class FarmPestSwatter : MonoBehaviour
{
    [Header("最小挥速（米/秒）")]
    public float minSwingSpeed = 1.5f;

    [Header("敲锣触发速度（比拍虫宽松，轻敲即可）")]
    public float gongSwingSpeed = 0.8f;

    Vector3 _lastPos;
    float _speed;
    bool _hasPos;

    void FixedUpdate()
    {
        if (_hasPos)
            _speed = (transform.position - _lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-5f);
        _lastPos = transform.position;
        _hasPos = true;
    }

    void OnTriggerStay(Collider other)
    {
        // 虫情锣：敲响开局
        var gong = other.GetComponentInParent<FarmPestGong>();
        if (gong != null)
        {
            if (_speed >= gongSwingSpeed) gong.Struck();
            return;
        }

        var pest = other.GetComponentInParent<FarmPest>();
        if (pest == null) return;
        if (FarmPestManager.Instance == null || !FarmPestManager.Instance.IsPlaying) return;
        if (_speed < minSwingSpeed) return;
        if (pest.Swat()) Haptic(0.6f);
    }

    void Haptic(float amp)
    {
        try
        {
            var dev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (dev.isValid) dev.SendHapticImpulse(0, Mathf.Clamp01(amp), 0.08f);
        }
        catch { /* 设备不可用时静默 */ }
    }
}

/// <summary>虫情锣：拍子敲响 = 开始/再来一轮虫情（带防连敲抖动）。</summary>
public class FarmPestGong : MonoBehaviour
{
    float _lastStrike;

    public void Struck()
    {
        if (Time.time - _lastStrike < 1.5f) return;
        _lastStrike = Time.time;
        if (FarmPestManager.Instance != null) FarmPestManager.Instance.OnGongStruck();
    }
}
