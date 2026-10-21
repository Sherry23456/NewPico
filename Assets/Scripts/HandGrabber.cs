using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

// 挂在左右手柄上：侧握（Grip）捡起范围内最近的 Grabbable，B/Y 键丢下
// 同时支持 Input System（含模拟器）和 XR 原生输入（PICO 真机）。
public class HandGrabber : MonoBehaviour
{
    [Header("这只手对应的节点")]
    public XRNode handNode = XRNode.RightHand;

    [Header("拾取范围（米，从手柄中心算）")]
    public float grabRadius = 0.45f;

    [Header("编辑器演示辅助半径（仅编辑器生效，真机仍用 grabRadius）")]
    public float editorAssistRadius = 1.5f;

    [Header("丢下时是否甩出")]
    public bool throwOnDrop = true;

    InputAction grabAction;
    InputAction gripValueAction;
    InputAction dropAction;
    Grabbable held;
    Vector3 lastPos;
    Vector3 velocity;
    bool velInit;
    bool wasDropPressed;
    bool waitForGripRelease;

    void Awake()
    {
        string hand = handNode == XRNode.LeftHand ? "LeftHand" : "RightHand";
        grabAction = new InputAction("Grab" + hand, InputActionType.Button, "<XRController>{" + hand + "}/gripButton");
        grabAction.AddBinding("<XRController>{" + hand + "}/gripPressed");
        gripValueAction = new InputAction("GripValue" + hand, InputActionType.Value, "<XRController>{" + hand + "}/grip");
        dropAction = new InputAction("Drop" + hand, InputActionType.Button, "<XRController>{" + hand + "}/secondaryButton");
    }

    void OnEnable()
    {
        grabAction.Enable();
        gripValueAction.Enable();
        dropAction.Enable();
        velInit = false;
        wasDropPressed = false;
        waitForGripRelease = false;
    }

    void OnDisable()
    {
        grabAction.Disable();
        gripValueAction.Disable();
        dropAction.Disable();
    }

    void OnDestroy()
    {
        grabAction.Dispose();
        gripValueAction.Dispose();
        dropAction.Dispose();
    }

    void LateUpdate()
    {
        bool gripPressed = grabAction.IsPressed() || gripValueAction.ReadValue<float>() >= 0.5f;
        bool dropPressed = dropAction.IsPressed();

        // 不依赖某一种 Input System 控件布局；手柄重连后也会重新取得设备。
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(handNode);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripButton))
                gripPressed |= gripButton;
            if (device.TryGetFeatureValue(CommonUsages.grip, out float grip))
                gripPressed |= grip >= 0.5f;
            if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondaryButton))
                dropPressed |= secondaryButton;
        }

        if (dropPressed && !wasDropPressed)
            TryDrop();
        wasDropPressed = dropPressed;

        if (!gripPressed) waitForGripRelease = false;
        // 握住后再靠近也能拿起；按 B/Y 放下后，须先松开握键才能再次拾取。
        if (gripPressed && !dropPressed && !waitForGripRelease && held == null)
            TryGrab();
    }

    void FixedUpdate()
    {
        // 记录手部速度，丢下时把速度传给物体
        Vector3 p = transform.position;
        if (!velInit)
        {
            lastPos = p;
            velInit = true;
            velocity = Vector3.zero;
            return;
        }
        velocity = (p - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPos = p;
    }

    // 捡起范围内最近的未持有 Grabbable（也可供其他系统调用）
    public bool TryGrab()
    {
        if (held != null) return false;
#if UNITY_EDITOR
        // 编辑器演示辅助：手不用真的够到地上的物体
        float radius = Mathf.Max(grabRadius, editorAssistRadius);
#else
        float radius = grabRadius;
#endif
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, Physics.AllLayers, QueryTriggerInteraction.Collide);
        Grabbable best = null;
        float bestDist = float.MaxValue;
        foreach (Collider col in hits)
        {
            Grabbable g = col.GetComponentInParent<Grabbable>();
            if (g == null || !g.isActiveAndEnabled || g.IsHeld) continue;
            Vector3 closest = col.ClosestPoint(transform.position);
            float d = (closest - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = g; }
        }
        if (best == null) return false;
        held = best;
        best.AttachTo(transform);
        return true;
    }

    // 丢下当前持有的物体（也可供其他系统调用）
    public bool TryDrop()
    {
        if (held == null) return false;
        Grabbable g = held;
        held = null;
        waitForGripRelease = true;
        g.Detach(throwOnDrop ? velocity : Vector3.zero);
        return true;
    }

    public Grabbable Held { get { return held; } }
}
