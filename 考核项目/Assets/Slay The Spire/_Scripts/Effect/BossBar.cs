using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public class BossBar : MonoBehaviour
{
    public Image fill;
    public float maxHP = 100;

    private float barWidth;
    private float barHeight;
    float curHP;

    void Start()
    {
        curHP = maxHP;
        barWidth = fill.rectTransform.rect.width;
        barHeight = fill.rectTransform.rect.height;
    }

    void Update()
    {
        HandleInput();
    }

    //受伤
    public void TakeDamage(float dmg)
    {
        if (curHP <= 0) return;
        var oldHP = curHP;
        curHP = Mathf.Max(0, curHP - dmg);

        var oldRatio = oldHP / maxHP;
        var newRatio = curHP / maxHP;

        fill.fillAmount = newRatio;
        SpawnChip(oldRatio, newRatio);
        SpawnFlash(newRatio);
    }

    //生成碎片
    private void SpawnChip(float oldRatio, float newRatio)
    {
        var chunk = ChipPool.Instance.GetChip();
        var img = chunk.GetComponent<Image>();
        var cg = chunk.GetComponent<CanvasGroup>();

        //处理碎片宽度，它 = 完整血条宽度 * 变化填充值，高度和原来一样
        img.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, barWidth * (oldRatio - newRatio));
        img.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, barHeight);

        //处理碎片位置：锚到fill右端
        var chunkRT = img.rectTransform;
        chunkRT.anchorMin = new Vector2(0, 0.5f);
        chunkRT.anchorMax = new Vector2(0, 0.5f);
        chunkRT.pivot = new Vector2(0, 0.5f);
        chunkRT.anchoredPosition = new Vector2(barWidth * newRatio, 0);
        cg.alpha = 1;

        //DOTween实现，慢慢下落 + 淡出
        chunkRT.DOAnchorPosY(-180f, 0.8f).SetEase(Ease.InCubic).SetDelay(0.10f);
        cg.DOFade(0, 0.8f).SetEase(Ease.InQuad).SetDelay(0.1f).OnComplete(() => ChipPool.Instance.ReturnChip(chunk));
    }

    //生成闪烁
    private void SpawnFlash(float newRatio)
    {
        var flashA = FlashPool.Instance.GetFlash();
        var fcg = flashA.GetComponent<CanvasGroup>();
        var flashRT = flashA.GetComponent<Image>().rectTransform;

        flashRT.anchoredPosition = new Vector2(barWidth * newRatio, 0);
        fcg.alpha = 1;
        fcg.DOFade(0, 0.3f).SetEase(Ease.InQuad).OnComplete(() => FlashPool.Instance.ReturnFlash(flashA));
    }

    //测试用，可删除 —— 模拟掉血
    private void HandleInput()
    {
        if (KeyDown(KeyCode.U)) TakeDamage(20f);
        if (KeyDown(KeyCode.I)) TakeDamage(Random.Range(0, 120f));
        if (KeyDown(KeyCode.O)) TakeDamage(Random.Range(0, 30f));
        if (KeyDown(KeyCode.E)) TakeDamage(Random.Range(0.5f, 2f));

        // 原来这段没有 if，每帧都执行，Boss 会被瞬间刷回满血（打出的牌看起来毫无效果）
        if (KeyDown(KeyCode.R))
        {
            curHP = maxHP;
            fill.DOFillAmount(1f, 0.5f).SetEase(Ease.OutQuad);
        }
    }

    // 项目用的是 Input System，老的 Input.GetKeyDown 会抛 InvalidOperationException，统一走新 API
    private static bool KeyDown(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;
        KeyControl key = code switch
        {
            KeyCode.U => keyboard.uKey,
            KeyCode.I => keyboard.iKey,
            KeyCode.O => keyboard.oKey,
            KeyCode.E => keyboard.eKey,
            KeyCode.R => keyboard.rKey,
            _ => null
        };
        return key != null && key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(code);
#endif
    }
}