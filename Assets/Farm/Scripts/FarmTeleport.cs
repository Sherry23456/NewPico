using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 右手柄拇指杆按下：射线瞄准地面显示落点圈，松开瞬移。
/// 策划案 §6 瞬移半边。
/// </summary>
public class FarmTeleport : MonoBehaviour
{
    public Transform hand;
    public float maxDistance = 12f;
    public float reticleRadius = 0.35f;
    public float maxSlopeAngle = 35f;

    InputAction _press;
    LineRenderer _line;
    GameObject _reticle;
    bool _valid;
    Vector3 _target;

    void Awake()
    {
        _press = new InputAction("FarmTP", InputActionType.Button, "<XRController>{RightHand}/primary2DAxisClick");
    }

    void OnEnable() => _press.Enable();
    void OnDisable() => _press.Disable();
    void OnDestroy() => _press.Dispose();

    void Start()
    {
        if (hand == null)
        {
            hand = transform.Find("Camera Offset/Right Controller");
            if (hand == null) hand = transform.Find("Camera Offset/RightHand Controller");
        }

        var lineGo = new GameObject("TPLine");
        _line = lineGo.AddComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.widthMultiplier = 0.012f;
        _line.useWorldSpace = true;
        _line.material = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.3f, 0.8f, 1f) };
        _line.enabled = false;
        lineGo.transform.SetParent(transform, false);

        _reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _reticle.name = "TPReticle";
        _reticle.transform.localScale = new Vector3(reticleRadius * 2f, 0.01f, reticleRadius * 2f);
        Destroy(_reticle.GetComponent<Collider>());
        _reticle.GetComponent<MeshRenderer>().material =
            new Material(Shader.Find("Unlit/Color")) { color = new Color(0.35f, 0.9f, 1f) };
        _reticle.SetActive(false);
    }

    void Update()
    {
        if (hand == null) return;
        bool held = _press.IsPressed();

        if (held)
        {
            var ray = new Ray(hand.position, hand.forward);
            _valid = Physics.Raycast(ray, out var hit, maxDistance)
                     && Vector3.Angle(hit.normal, Vector3.up) < maxSlopeAngle;
            _line.enabled = true;
            _line.SetPosition(0, hand.position);
            if (_valid)
            {
                _target = hit.point;
                _line.SetPosition(1, _target);
                _reticle.SetActive(true);
                _reticle.transform.position = _target + Vector3.up * 0.02f;
            }
            else
            {
                _line.SetPosition(1, ray.origin + ray.direction * maxDistance);
                _reticle.SetActive(false);
            }
        }
        else if (_line.enabled)
        {
            _line.enabled = false;
            _reticle.SetActive(false);
            if (_valid)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    Vector3 foot = cam.transform.position;
                    foot.y = transform.position.y;
                    transform.position += _target - foot;
                }
                _valid = false;
            }
        }
    }
}
