using System.Collections.Generic;
using UnityEngine;

// 可拾取物：挂在被抓取物体的根节点上，定义被拿在手里时的姿态
// 任意新物体挂上本组件 + Collider/Rigidbody，即可被 HandGrabber 捡起
public class Grabbable : MonoBehaviour
{
    public static readonly List<Grabbable> All = new List<Grabbable>();

    [Header("拿在手里时相对手柄的位姿")]
    public Vector3 heldLocalPosition = new Vector3(0f, -0.02f, 0.03f);
    public Vector3 heldLocalEuler = new Vector3(-25f, 90f, 0f);

    [Header("丢下时是否继承手部速度（可甩出去）")]
    public bool throwOnDrop = true;

    [Header("初始是否冻结物理（保证摆放姿态精确，首次捡起后恢复正常）")]
    public bool startKinematic = false;

    public bool IsHeld { get; private set; }

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (startKinematic && rb != null) rb.isKinematic = true;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // 由 HandGrabber 调用：固定到手柄
    public void AttachTo(Transform hand)
    {
        IsHeld = true;
        if (rb != null) rb.isKinematic = true;
        transform.SetParent(hand, true);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.Euler(heldLocalEuler);
    }

    // 由 HandGrabber 调用：丢回世界
    public void Detach(Vector3 throwVelocity)
    {
        IsHeld = false;
        transform.SetParent(null, true);
        if (rb != null)
        {
            rb.isKinematic = false;
            if (throwOnDrop) rb.velocity = throwVelocity;
        }
    }
}
