using UnityEngine;

// 挂在锤头触发器上：挥动速度够快才算"敲"，防止贴着洞口刷分
public class HammerHit : MonoBehaviour
{
    [Header("最小挥动速度（米/秒）")]
    public float minSwingSpeed = 1.5f;

    Vector3 lastPos;
    float speed;
    bool initialized;

    void FixedUpdate()
    {
        Vector3 p = transform.position;
        if (!initialized)
        {
            lastPos = p;
            initialized = true;
            speed = 0f;
            return;
        }
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        speed = (p - lastPos).magnitude / dt;
        lastPos = p;
    }

    void OnTriggerStay(Collider other)
    {
        Mole mole = other.GetComponentInParent<Mole>();
        if (mole == null) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        if (speed < minSwingSpeed) return;
        mole.Hit();
    }
}
