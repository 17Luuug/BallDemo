using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class CardView : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text mana;
    [SerializeField] private SpriteRenderer imageSR;
    [SerializeField] private GameObject wrapper;
    [SerializeField] private LayerMask dropLayer;

    [Header("拖拽")]
    [Tooltip("拖拽时卡牌的放大倍率（相对它悬浮在手牌上方时的大小）")]
    [SerializeField] private float dragScaleMultiplier = 1.05f;
    [Tooltip("拖拽时使用的渲染层级")]
    [SerializeField] private int dragSortingOrder = 100;

    private SortingGroup sortingGroup;

    public Card Card { get; private set; }

    /// <summary>正在被玩家拖拽，手牌布局会跳过它。</summary>
    public bool IsBeingDragged { get; private set; }

    void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
        if (sortingGroup == null) sortingGroup = gameObject.AddComponent<SortingGroup>();
    }

    public void Setup(Card card)
    {
        Card = card;
        title.text = card.Title;
        description.text = card.Description;
        mana.text = card.Mana.ToString();
        imageSR.sprite = card.Image;
    }

    public void SetSortingOrder(int order)
    {
        sortingGroup.sortingOrder = order;
    }

    // ---------- 悬浮 ----------

    void OnMouseEnter()
    {
        if (Interactions.Instance == null || !Interactions.Instance.PlayerCanHover()) return;
        if (IsBeingDragged) return;
        HandView.Instance?.OnCardHover(this);
    }

    void OnMouseExit()
    {
        // 不加 CanHover 守卫：拖拽中也要能把悬浮状态收掉，否则会卡住
        if (HandView.Instance != null && HandView.Instance.IsHovered(this))
        {
            HandView.Instance.OnCardUnhover(this);
        }
    }

    // ---------- 拖拽 ----------

    void OnMouseDown()
    {
        if (Interactions.Instance == null || !Interactions.Instance.PlayerCanInteract()) return;

        IsBeingDragged = true;
        Interactions.Instance.PlayerIsDragging = true;
        HandView.Instance?.ClearHover();

        SetSortingOrder(dragSortingOrder);
        transform.DOKill();
        transform.DORotate(Vector3.zero, 0.1f);
        transform.DOScale(Vector3.one * GetDragScale(), 0.12f).SetEase(Ease.OutQuad);
        transform.position = MouseUtil.GetMousePositionInWorldSpace(-1);
    }

    void OnMouseDrag()
    {
        if (Interactions.Instance == null || !Interactions.Instance.PlayerCanInteract()) return;
        transform.position = MouseUtil.GetMousePositionInWorldSpace(-1);
    }

    void OnMouseUp()
    {
        IsBeingDragged = false;
        if (Interactions.Instance != null) Interactions.Instance.PlayerIsDragging = false;

        bool canInteract = Interactions.Instance == null || Interactions.Instance.PlayerCanInteract();
        bool droppedOnPlayArea = Physics.Raycast(transform.position, Vector3.forward, out RaycastHit hit, 10f, dropLayer);
        bool hasMana = ManaSystem.Instance != null && ManaSystem.Instance.HasEnoughMana(Card.Mana);

        if (canInteract && hasMana && droppedOnPlayArea)
        {
            PlayCardGA playCardGA = new(Card);
            ActionSystem.Instance.Perform(playCardGA);
            return;
        }

        // 没打出去，丝滑地飘回手牌
        SetSortingOrder(0);
        HandView.Instance?.ReturnCardToHand(this);
    }

    private float GetDragScale()
    {
        float baseScale = HandView.Instance != null ? HandView.Instance.HoverCardScale : 0.75f;
        return baseScale * dragScaleMultiplier;
    }
}
