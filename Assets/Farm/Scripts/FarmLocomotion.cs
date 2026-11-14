using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 左摇杆平滑移动（头显朝向为前进基准 + 死区 + 速度渐变 + 重力）。
/// 策划案 §6 平移半边，参考官方教程实现并补齐重力。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FarmLocomotion : MonoBehaviour
{
    [Header("移动")] public float moveSpeed = 1.8f;
    [Range(0f, 0.5f)] public float deadzone = 0.15f;
    [Header("重力")] public float gravity = -9.8f;

    CharacterController _cc;
    InputAction _move;
    Transform _cam;
    float _smoothSpeed;
    float _vy;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _move = new InputAction("FarmMove", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
        // 键鼠备用：WASD + 方向键（模拟器已在 PC 模式禁用，不再抢键）
        var wasd = _move.AddCompositeBinding("2DVector");
        wasd.With("Up", "<Keyboard>/w");
        wasd.With("Down", "<Keyboard>/s");
        wasd.With("Left", "<Keyboard>/a");
        wasd.With("Right", "<Keyboard>/d");
        var arrows = _move.AddCompositeBinding("2DVector");
        arrows.With("Up", "<Keyboard>/upArrow");
        arrows.With("Down", "<Keyboard>/downArrow");
        arrows.With("Left", "<Keyboard>/leftArrow");
        arrows.With("Right", "<Keyboard>/rightArrow");
    }

    void OnEnable() => _move.Enable();
    void OnDisable() => _move.Disable();
    void OnDestroy() => _move.Dispose();

    void Start()
    {
        var cam = GetComponentInChildren<Camera>();
        if (cam != null) _cam = cam.transform;
    }

    void Update()
    {
        if (_cam == null) return;

        Vector2 ax = _move.ReadValue<Vector2>();
        if (ax.magnitude < deadzone) ax = Vector2.zero;

        Vector3 fwd = Vector3.ProjectOnPlane(_cam.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(_cam.right, Vector3.up).normalized;
        Vector3 dir = fwd * ax.y + right * ax.x;

        float target = Mathf.Clamp01(ax.magnitude) * moveSpeed;
        _smoothSpeed = Mathf.Lerp(_smoothSpeed, target, 5f * Time.deltaTime);

        if (_cc.isGrounded) _vy = -0.5f;
        else _vy = Mathf.Max(_vy + gravity * Time.deltaTime, -25f);

        Vector3 motion = dir * _smoothSpeed + Vector3.up * _vy;
        _cc.Move(motion * Time.deltaTime);

        // 防坠安全网：异常掉落/飞出时拉回地面
        var p = transform.position;
        if (p.y < -3f || p.y > 30f)
        {
            transform.position = new Vector3(p.x, 0.1f, p.z);
            _vy = -0.5f;
        }
    }
}
