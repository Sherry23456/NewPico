using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// PC 模式：无 HMD 时自动启用——禁用 XRI 设备模拟器，提供鼠标视角（按住右键拖动）。
/// 有头显时自动休眠，不影响 VR 输入。
/// </summary>
public class FarmPC : MonoBehaviour
{
    public static bool PCMode { get; private set; }

    Transform _head;
    Transform _offset;
    float _pitch;

    void Start()
    {
        PCMode = !XRSettings.isDeviceActive;
        if (!PCMode) { enabled = false; return; }

        // 禁用 XRI 设备模拟器（会抢 WASD/E/鼠标按键）
        foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            var tn = mb.GetType().Name;
            if (tn.Contains("Simulator") || tn.Contains("SimulatedDevice"))
                mb.gameObject.SetActive(false);
        }

        var rig = GameObject.Find("XR Origin (XR Rig)");
        if (rig == null) { enabled = false; return; }
        _offset = rig.transform.Find("Camera Offset");
        if (_offset != null) _head = _offset.Find("Main Camera");
        if (_head == null) { enabled = false; return; }

        Debug.Log("[FarmPC] PC 模式启用：WASD/方向键移动，右键拖动转视角，左键交互");
    }

    void Update()
    {
        if (_offset == null || Mouse.current == null) return;
        if (Mouse.current.rightButton.isPressed)
        {
            var d = Mouse.current.delta.ReadValue() * 0.12f;
            var e = _offset.localEulerAngles;
            _offset.localEulerAngles = new Vector3(0f, e.y - d.x, 0f);
            _pitch = Mathf.Clamp(_pitch - d.y, -80f, 80f);
            _head.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }
    }
}
