using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 战斗单位（英雄 / 敌人的公共父类）：管理血量，以及**排在人物下方一条水平线上**的血条。
/// 血量文字嵌在血条内部，所以不再单独显示原始的那个文字节点。
/// </summary>
public class CombatantView : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("血条设置")]
    [Tooltip("所有角色的血条统一排在这条水平线的世界 Y 坐标（当前三人物的脚底在 -1.38 ~ -1.81 之间）")]
    [SerializeField] private float healthBarWorldY = -1.70f;
    [Tooltip("血条在世界空间里的宽度，所有角色统一，视觉上更整齐")]
    [SerializeField] private float healthBarWidth = 2.6f;
    [Tooltip("创建血条后隐藏原本独立的血量文字节点（文字已经嵌进血条了）")]
    [SerializeField] private bool hideOriginalHealthText = true;

    private HealthBar healthBar;

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }

    protected void SetupBase(int health, Sprite image)
    {
        MaxHealth = CurrentHealth = health;
        spriteRenderer.sprite = image;

        EnsureHealthBar();
        UpdateHealthText();
        healthBar.SetHealth(CurrentHealth, MaxHealth, true);   // 初始化时直接显示满血
    }

    private void UpdateHealthText()
    {
        string display = CurrentHealth + " / " + MaxHealth;

        if (healthText != null)
        {
            healthText.text = display;
        }

        // 真正显示给玩家看的是血条里那一份
        if (healthBar != null)
        {
            healthBar.SetLabel(display);
        }
    }

    public virtual void Damage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
        if (CurrentHealth < 0)
        {
            CurrentHealth = 0;
        }
        transform.DOShakePosition(0.2f, 0.5f);
        UpdateHealthText();

        if (healthBar != null)
        {
            healthBar.SetHealth(CurrentHealth, MaxHealth);   // 血条跟着掉
        }
    }

    public virtual void Heal(int healAmount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healAmount);
        UpdateHealthText();

        if (healthBar != null)
        {
            healthBar.SetHealth(CurrentHealth, MaxHealth);   // 血条跟着涨
        }
    }

    /// <summary>
    /// 创建血条：横向对准人物可见区域的中线，纵向统一落在 healthBarWorldY 这条水平线上。
    /// 挂在血量文字的父节点（即 CombatantViewBase）下，所以受击抖动、敌人前冲时血条会跟着走。
    /// </summary>
    private void EnsureHealthBar()
    {
        if (healthBar != null) return;

        Transform parent = healthText != null ? healthText.transform.parent : transform;
        Transform spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;

        // 人物可见区域的中线。按精灵的 rect + pivot 反算，不依赖 Renderer.bounds
        // —— bounds 是缓存值，刚赋完 sprite 就读取可能还是上一帧的。
        float centerX = spriteTransform.position.x;
        Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (sprite != null)
        {
            float ppu = sprite.pixelsPerUnit;
            if (ppu < 1e-4f) ppu = 100f;
            Rect rect = sprite.rect;
            Vector2 pivot = sprite.pivot;
            centerX = spriteTransform.position.x
                      + ((rect.width * 0.5f - pivot.x) / ppu) * spriteTransform.lossyScale.x;
        }

        Vector3 worldPoint = new Vector3(centerX, healthBarWorldY, spriteTransform.position.z);
        Vector3 localPoint = parent.InverseTransformPoint(worldPoint);
        localPoint.z = 0f;

        TMP_FontAsset font = healthText != null ? healthText.font : null;
        healthBar = HealthBar.Create(parent, localPoint, healthBarWidth, font);

        // 文字已经在血条里了，原始的那个独立文字节点收起来
        if (hideOriginalHealthText && healthText != null)
        {
            healthText.gameObject.SetActive(false);
        }
    }
}
