using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

public class HandView : Singleton<HandView>
{
    [Header("手牌排布")]
    [SerializeField] private SplineContainer splineContainer;
    [Tooltip("相邻两张牌在曲线上的间距（相对整条曲线）")]
    [SerializeField] private float cardSpacing = 0.1f;

    [Header("卡牌大小")]
    [Tooltip("卡牌待在手里时的大小")]
    [SerializeField] private float handCardScale = 0.55f;
    [Tooltip("卡牌悬浮在手牌上方时的大小（必须大于手里的大小，否则鼠标会滑出卡牌导致闪烁）")]
    [SerializeField] private float hoverCardScale = 0.75f;

    [Header("悬浮动画")]
    [Tooltip("悬浮时向上浮起的高度，上限约 (悬浮缩放/手里缩放 - 1) x 半张牌高")]
    [SerializeField] private float hoverRise = 0.35f;
    [Tooltip("相邻卡牌向两侧让开的距离")]
    [SerializeField] private float hoverFanSpread = 0.45f;
    [Tooltip("悬浮时使用的渲染层级，保证盖住其他手牌")]
    [SerializeField] private int hoverSortingOrder = 50;

    [Header("动画时长")]
    [Tooltip("抽牌、出牌后重新排布的时长")]
    [SerializeField] private float layoutDuration = 0.15f;
    [Tooltip("悬浮/取消悬浮的时长")]
    [SerializeField] private float hoverDuration = 0.14f;

    private readonly List<CardView> cards = new();
    private CardView hoveredCard;

    /// <summary>卡牌待在手里时的大小，CardViewCreator 生成卡牌时会用到。</summary>
    public float HandCardScale => handCardScale;

    /// <summary>卡牌悬浮/被拿起时的大小。</summary>
    public float HoverCardScale => hoverCardScale;

    public bool IsHovered(CardView cardView) => hoveredCard == cardView;

    public IEnumerator AddCard(CardView cardView)
    {
        cards.Add(cardView);
        yield return UpdataCardPositions(layoutDuration);
    }

    public CardView RemoveCard(Card card)
    {
        CardView cardView = GetCardView(card);
        if (cardView == null) return null;

        cards.Remove(cardView);
        if (hoveredCard == cardView) hoveredCard = null;
        cardView.SetSortingOrder(0);

        StartCoroutine(UpdataCardPositions(layoutDuration));
        return cardView;
    }

    private CardView GetCardView(Card card)
    {
        return cards.Where(cardView => cardView.Card == card).FirstOrDefault();
    }

    // ---------- 悬浮 ----------

    /// <summary>鼠标进入某张手牌：这张牌浮起来，相邻的牌让开。</summary>
    public void OnCardHover(CardView cardView)
    {
        if (cardView == null || hoveredCard == cardView) return;
        hoveredCard = cardView;
        ApplyLayout(hoverDuration);
    }

    /// <summary>鼠标离开某张手牌：回到手里的位置。</summary>
    public void OnCardUnhover(CardView cardView)
    {
        if (hoveredCard != cardView) return;
        hoveredCard = null;
        ApplyLayout(hoverDuration);
    }

    /// <summary>把当前悬浮的牌收回去（开始拖拽、抽牌等场合调用）。</summary>
    public void ClearHover()
    {
        if (hoveredCard == null) return;
        hoveredCard = null;
        ApplyLayout(hoverDuration);
    }

    /// <summary>拖拽失败时，卡牌丝滑地飘回手牌。</summary>
    public void ReturnCardToHand(CardView cardView)
    {
        if (hoveredCard == cardView) hoveredCard = null;
        ApplyLayout(layoutDuration * 2f);
    }

    // ---------- 排布 ----------

    private IEnumerator UpdataCardPositions(float duration)
    {
        ApplyLayout(duration);
        yield return new WaitForSeconds(duration);
    }

    private void ApplyLayout(float duration)
    {
        if (cards.Count == 0) return;
        if (splineContainer == null || splineContainer.Spline == null) return;

        Spline spline = splineContainer.Spline;
        int count = cards.Count;

        // 手牌越多间距越密，保证不会挤出曲线两端
        float spacing = Mathf.Min(cardSpacing, 1f / Mathf.Max(count, 1));
        float firstCardPosition = 0.5f - (count - 1) * spacing / 2f;
        int hoverIndex = hoveredCard != null ? cards.IndexOf(hoveredCard) : -1;

        for (int i = 0; i < count; i++)
        {
            CardView cardView = cards[i];
            if (cardView == null) continue;

            // 正在被拖拽的牌由 CardView 自己控制位置
            if (cardView.IsBeingDragged) continue;

            float p = firstCardPosition + i * spacing;
            Vector3 splinePosition = spline.EvaluatePosition(p);
            Vector3 forward = spline.EvaluateTangent(p);
            Vector3 up = spline.EvaluateUpVector(p);
            Quaternion rotation = Quaternion.LookRotation(up, Vector3.Cross(up, forward).normalized);

            Vector3 targetPosition = splinePosition + transform.position + 0.01f * i * Vector3.back;
            float targetScale = handCardScale;
            int sortingOrder = 0;

            if (hoverIndex >= 0)
            {
                if (i == hoverIndex)
                {
                    targetPosition += Vector3.up * hoverRise;
                    targetScale = hoverCardScale;
                    sortingOrder = hoverSortingOrder;
                }
                else
                {
                    // 离悬浮卡越远的牌让得越少
                    Vector3 tangent = ((Vector3)forward).normalized;
                    float falloff = 1f / Mathf.Abs(i - hoverIndex);
                    targetPosition += tangent * (i < hoverIndex ? -1f : 1f) * hoverFanSpread * falloff;
                }
            }

            cardView.SetSortingOrder(sortingOrder);
            cardView.transform.DOKill();
            cardView.transform.DOMove(targetPosition, duration).SetEase(Ease.OutCubic);
            cardView.transform.DORotate(rotation.eulerAngles, duration).SetEase(Ease.OutCubic);
            cardView.transform.DOScale(Vector3.one * targetScale, duration).SetEase(Ease.OutQuad);
        }
    }
}
