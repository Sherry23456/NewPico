using System.Collections;
using UnityEngine;

public enum PestState { Hidden, Sitting, Hit }
public enum PestExit { Swatted, Cleared }

/// <summary>
/// 一只藏在植株间的虫：本体就是池化实例（模板根上挂 FarmPest + FarmPestVariant）。
/// 不冒头不移动，靠玩家走进田里观察发现后拍除；轻微摇摆提示"是活物"。
/// 拍中压扁动画后归还 PrefabPool，全程零 Destroy。
/// </summary>
public class FarmPest : MonoBehaviour
{
    [Header("压扁动画时长")]
    public float hitAnimTime = 0.15f;

    [Header("待机摇摆幅度（度）")]
    public float swayDegrees = 5f;

    public PestState State { get; private set; } = PestState.Hidden;
    public bool IsIdle { get { return State == PestState.Hidden; } }

    /// <summary>自己的变体数据（模板根上，实例化后仍在）。</summary>
    public FarmPestVariant Variant { get { return GetComponent<FarmPestVariant>(); } }

    /// <summary>拍中回调（Manager 在投放时挂上）。</summary>
    public System.Action<FarmPest, PestExit, float> onRetired;

    Quaternion _baseRot;
    float _swayPhase;

    void Update()
    {
        if (State != PestState.Sitting || swayDegrees <= 0f) return;
        _swayPhase += Time.deltaTime * 2.2f;
        float yaw = Mathf.Sin(_swayPhase) * swayDegrees;
        transform.rotation = _baseRot * Quaternion.Euler(0f, yaw, 0f);
    }

    /// <summary>投放到田间藏匿点：就位 + 确保拍击检测用的 trigger 碰撞体。</summary>
    public void Place(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        _baseRot = rot;
        transform.rotation = rot;
        _swayPhase = Random.value * Mathf.PI * 2f;
        var col = GetComponent<Collider>();
        if (col == null)
        {
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.16f;
        }
        else col.isTrigger = true;
        State = PestState.Sitting;
    }

    /// <summary>拍中。仅 Sitting 状态生效。返回是否命中。</summary>
    public bool Swat()
    {
        if (State != PestState.Sitting) return false;
        State = PestState.Hit;
        StartCoroutine(SquashThenRetire());
        return true;
    }

    IEnumerator SquashThenRetire()
    {
        float t = 0f;
        Vector3 baseScale = transform.localScale;
        while (t < hitAnimTime)
        {
            t += Time.deltaTime;
            Vector3 s = baseScale;
            s.y = baseScale.y * Mathf.Lerp(1f, 0.12f, Mathf.Clamp01(t / hitAnimTime));
            transform.localScale = s;
            yield return null;
        }
        Retire();
    }

    void Retire()
    {
        var cb = onRetired;
        onRetired = null;
        PrefabPool.Release(gameObject);
        State = PestState.Hidden;
        if (cb != null) cb(this, PestExit.Swatted, 0f);
    }

    /// <summary>轮次结束清场（不触发回调）。</summary>
    public void Despawn()
    {
        StopAllCoroutines();
        if (State != PestState.Hidden)
        {
            PrefabPool.Release(gameObject);
            State = PestState.Hidden;
        }
        onRetired = null;
    }
}
