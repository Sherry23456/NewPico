using System.Collections;
using UnityEngine;

public enum MoleState { Hidden, Rising, Up, Hiding, Hit }

// 洞里的一个出生点：负责冒头/缩回/被打的状态机，具体模型由 MoleSpawner 换装
public class Mole : MonoBehaviour
{
    public MoleState State { get; private set; } = MoleState.Hidden;

    [Header("冒头高度：相对隐藏位置抬升的距离")]
    public float riseHeight = 0.28f;
    public float riseTime = 0.18f;
    public float hideTime = 0.22f;
    public float hitAnimTime = 0.25f;

    Transform visual;
    Coroutine routine;
    float hiddenY;
    float stayTime;
    System.Action<Mole> onRetired;

    public bool IsIdle { get { return State == MoleState.Hidden; } }

    void Awake()
    {
        hiddenY = transform.localPosition.y;
    }

    void Start()
    {
        SetY(hiddenY);
    }

    // 由生成器调用：换上变体模型并开始冒头
    public void Show(GameObject variantPrefab, float stay, System.Action<Mole> retiredCallback)
    {
        ClearVisual();
        if (variantPrefab != null)
        {
            GameObject go = Instantiate(variantPrefab, transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            visual = go.transform;
        }
        onRetired = retiredCallback;
        stayTime = stay;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RiseAndFall());
    }

    // 回合结束/重开时平滑缩回
    public void HideNow()
    {
        if (State == MoleState.Hidden) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Hide());
    }

    // 立即归位（开始新一局时用）
    public void ForceReset()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        State = MoleState.Hidden;
        SetY(hiddenY);
        ClearVisual();
        onRetired = null;
    }

    // 由 HammerHit 调用；只有冒头状态能被打中，状态上锁防重复计分
    public void Hit()
    {
        if (State != MoleState.Up) return;
        State = MoleState.Hit;
        if (routine != null) StopCoroutine(routine);

        MoleVariant variant = visual != null ? visual.GetComponent<MoleVariant>() : null;
        if (variant != null)
        {
            if (variant.isBomb && variant.explosionPrefab != null)
            {
                // 炸弹：原地爆炸并直接炸掉模型
                Instantiate(variant.explosionPrefab, transform.position, Quaternion.identity);
                ClearVisual();
            }
            else
            {
                StartCoroutine(Squash());
            }
            if (variant.hitSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlayOneShot(variant.hitSound);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(variant.score);
                if (variant.isBomb) GameManager.Instance.OnBombHit();
            }
        }

        routine = StartCoroutine(HideAfterHit());
    }

    IEnumerator RiseAndFall()
    {
        State = MoleState.Rising;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPop();
        yield return MoveY(hiddenY, hiddenY + riseHeight, riseTime);
        State = MoleState.Up;
        yield return new WaitForSeconds(stayTime);
        yield return Hide();
    }

    IEnumerator Hide()
    {
        State = MoleState.Hiding;
        yield return MoveY(transform.localPosition.y, hiddenY, hideTime);
        Retire();
    }

    IEnumerator HideAfterHit()
    {
        yield return new WaitForSeconds(hitAnimTime);
        yield return Hide();
    }

    IEnumerator MoveY(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetY(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetY(to);
    }

    // 被打扁：压扁 + 略微鼓边
    IEnumerator Squash()
    {
        Transform v = visual;
        if (v == null) yield break;
        Vector3 s = v.localScale;
        float t = 0f;
        while (t < 0.12f && v != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.12f);
            v.localScale = new Vector3(s.x * (1f + 0.5f * k), s.y * Mathf.Lerp(1f, 0.35f, k), s.z * (1f + 0.5f * k));
            yield return null;
        }
    }

    void Retire()
    {
        State = MoleState.Hidden;
        SetY(hiddenY);
        ClearVisual();
        var cb = onRetired;
        onRetired = null;
        if (cb != null) cb(this);
    }

    void ClearVisual()
    {
        if (visual != null)
        {
            Destroy(visual.gameObject);
            visual = null;
        }
    }

    void SetY(float y)
    {
        Vector3 p = transform.localPosition;
        p.y = y;
        transform.localPosition = p;
    }
}
