using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗单位（英雄 / 敌人）头顶的世界空间血条。
///
/// 由 <see cref="CombatantView"/> 在初始化时自动创建，不需要在 Prefab / 场景里手动搭 UI。
/// 表现：
///   · 受到攻击掉血时，红色前景立刻缩短；
///   · 后面留一条黄色的“残影”，稍作停顿后慢慢跟上，形成明显的掉血反馈；
///   · 掉血瞬间在血条前端闪一条白色高光。
/// </summary>
[DisallowMultipleComponent]
public class HealthBar : MonoBehaviour
{
    // Canvas 内部设计尺寸（像素）。真实大小由 RectTransform 的缩放决定，
    // 所以修改这两个值只影响“宽高比例”，不影响实际大小。
    private const float DesignWidth = 200f;
    private const float DesignHeight = 22f;

    /// <summary>血条高度 / 宽度。外部需要按宽度推算高度时用它。</summary>
    public const float HeightPerWidth = DesignHeight / DesignWidth;

    [Header("颜色")]
    [SerializeField] private Color frameColor = new Color(0.04f, 0.04f, 0.06f, 0.95f);
    [SerializeField] private Color backColor = new Color(0.26f, 0.26f, 0.30f, 1f);
    [SerializeField] private Color fillColor = new Color(0.85f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color delayColor = new Color(1f, 0.85f, 0.35f, 1f);

    [Header("动画时长（秒）")]
    [SerializeField] private float fillDuration = 0.18f;
    [SerializeField] private float delayHold = 0.22f;
    [SerializeField] private float delayDuration = 0.40f;

    [Header("血量文字（嵌在血条里）")]
    [Tooltip("文字颜色。血条底色偏深，默认用白色")]
    [SerializeField] private Color labelColor = Color.white;
    [Tooltip("文字左右两侧留出的内边距（设计像素）")]
    [SerializeField] private float labelPadding = 8f;

    private TextMeshProUGUI label;

    private Image fillImage;
    private Image delayImage;
    private Image sparkImage;

    private Tween fillTween;
    private Tween delayTween;
    private Tween sparkTween;

    private float ratio = 1f;
    private bool built;

    /// <summary>血条当前显示的比例（0~1）。</summary>
    public float Ratio => ratio;

    // 一张 1x1 的白色精灵，所有色块共用。Image 用 Filled 类型时必须有 sprite，
    // 否则 Unity 会退化成普通矩形，fillAmount 不生效。
    private static Sprite whiteSprite;

    private static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;

                whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
                whiteSprite.name = "HealthBar_White";
                whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            }

            return whiteSprite;
        }
    }

    // ------------------------------------------------------------------
    // 创建
    // ------------------------------------------------------------------

    /// <summary>
    /// 在 parent 下创建一个血条。
    /// </summary>
    /// <param name="parent">挂载父节点（会自动抵消父节点缩放）。</param>
    /// <param name="localPosition">相对父节点的位置。</param>
    /// <param name="worldWidth">血条在世界空间里的宽度。</param>
    /// <param name="font">血量文字用的字体，通常从场景里原有的血量文字上取，保证字形一致。</param>
    public static HealthBar Create(Transform parent, Vector3 localPosition, float worldWidth, TMP_FontAsset font = null)
    {
        float parentScale = Mathf.Abs(parent.lossyScale.x);
        if (parentScale < 1e-4f) parentScale = 1f;

        GameObject go = new GameObject("HealthBar", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(DesignWidth, DesignHeight);

        // 位置和缩放放在最后设置，避免被锚点/尺寸的赋值覆盖
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * (worldWidth / parentScale / DesignWidth);
        go.transform.localPosition = localPosition;

        HealthBar bar = go.AddComponent<HealthBar>();
        bar.Build(font);
        return bar;
    }

    private void Build(TMP_FontAsset font = null)
    {
        if (built) return;
        built = true;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;   // 盖在角色与打击特效之上

        // 1) 外框（比底槽大一圈，形成描边）
        Image frame = CreateImage("Frame", transform, frameColor);
        frame.rectTransform.offsetMin = new Vector2(-2.5f, -2.5f);
        frame.rectTransform.offsetMax = new Vector2(2.5f, 2.5f);

        // 2) 底槽
        CreateImage("Back", transform, backColor);

        // 3) 黄色残影
        delayImage = CreateImage("Delay", transform, delayColor);
        MakeFilled(delayImage, 1f);

        // 4) 红色前景
        fillImage = CreateImage("Fill", transform, fillColor);
        MakeFilled(fillImage, 1f);

        // 5) 前端白色高光
        sparkImage = CreateImage("Spark", transform, new Color(1f, 1f, 1f, 0f));
        RectTransform sparkRT = sparkImage.rectTransform;
        sparkRT.anchorMin = sparkRT.anchorMax = new Vector2(0f, 0.5f);
        sparkRT.pivot = new Vector2(0.5f, 0.5f);
        sparkRT.sizeDelta = new Vector2(6f, DesignHeight);
        sparkRT.anchoredPosition = new Vector2(DesignWidth, 0f);

        // 6) 血量文字。必须是最后一个子节点，Canvas 内按同级顺序绘制，这样它压在所有色块之上。
        label = CreateLabel(font);
    }

    /// <summary>
    /// 创建嵌在血条里的血量文字。
    /// 用 TextMeshProUGUI（UI 版）而不是 TextMeshPro（3D 版）：血条自己带一个 WorldSpace Canvas，
    /// 把 3D 版文字塞进 Canvas 下并不会被 Canvas 接管渲染，只有 UI 版才会跟随 Canvas 的层级。
    /// 字号交给 TMP 自动缩放（enableAutoSizing），所以不必手动换算设计像素 ↔ 世界单位。
    /// </summary>
    private TextMeshProUGUI CreateLabel(TMP_FontAsset font)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(labelPadding, 0f);
        rt.offsetMax = new Vector2(-labelPadding, 0f);
        rt.localScale = Vector3.one;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;

        text.text = string.Empty;
        text.color = labelColor;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 4f;
        text.fontSizeMax = DesignHeight;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;      // 别挡住手牌的鼠标判定
        text.margin = Vector4.zero;
        return text;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        Image img = go.AddComponent<Image>();
        img.sprite = WhiteSprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void MakeFilled(Image img, float fill)
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = fill;
    }

    // ------------------------------------------------------------------
    // 对外接口
    // ------------------------------------------------------------------

    /// <summary>设置血条上显示的文字（例如 "55 / 55"）。</summary>
    public void SetLabel(string value)
    {
        if (!built) Build();
        if (label != null) label.text = value;
    }

    /// <summary>设置血量，掉血时播放动画。</summary>
    public void SetHealth(int current, int max)
    {
        SetRatio(max <= 0 ? 0f : (float)current / max, false);
    }

    /// <summary>设置血量。instant = true 时不做动画，直接跳到目标值（初始化时用）。</summary>
    public void SetHealth(int current, int max, bool instant)
    {
        SetRatio(max <= 0 ? 0f : (float)current / max, instant);
    }

    /// <summary>直接按比例（0~1）设置。</summary>
    public void SetRatio(float newRatio, bool instant)
    {
        if (!built) Build();

        newRatio = Mathf.Clamp01(newRatio);
        bool losing = newRatio < ratio - 0.0001f;
        ratio = newRatio;

        KillTweens();

        if (instant || !Application.isPlaying)
        {
            fillImage.fillAmount = newRatio;
            delayImage.fillAmount = newRatio;
            sparkImage.color = new Color(1f, 1f, 1f, 0f);
            return;
        }

        // 前景：快速滑到新血量
        fillTween = fillImage.DOFillAmount(newRatio, fillDuration).SetEase(Ease.OutQuad);

        if (losing)
        {
            // 残影：先停在原来的位置，等一小会儿再慢慢缩到新值
            delayImage.fillAmount = Mathf.Max(delayImage.fillAmount, newRatio);
            delayTween = delayImage.DOFillAmount(newRatio, delayDuration)
                                   .SetEase(Ease.InOutQuad)
                                   .SetDelay(delayHold);
            PlaySpark(newRatio);
        }
        else
        {
            delayImage.fillAmount = newRatio;
        }
    }

    private void PlaySpark(float atRatio)
    {
        sparkImage.rectTransform.anchoredPosition = new Vector2(DesignWidth * atRatio, 0f);
        sparkImage.color = new Color(1f, 1f, 1f, 0.85f);
        sparkTween = sparkImage.DOFade(0f, 0.30f).SetEase(Ease.OutQuad);
    }

    private void KillTweens()
    {
        fillTween?.Kill();
        delayTween?.Kill();
        sparkTween?.Kill();
        fillTween = null;
        delayTween = null;
        sparkTween = null;
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}
