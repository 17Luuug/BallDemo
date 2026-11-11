using UnityEngine;

public class CardViewCreator : Singleton<CardViewCreator>
{
    [Tooltip("刚生成时的大小，相对手牌中的大小（小一点会有一个抽牌时长大的效果）")]
    [SerializeField] private float spawnScaleRatio = 0.7f;

    [SerializeField] private CardView cardViewPrefab;

    public CardView CreateCardView(Card card, Vector3 position, Quaternion rotation)
    {
        CardView cardView = Instantiate(cardViewPrefab, position, rotation);
        cardView.transform.localScale = Vector3.one * GetHandScale() * spawnScaleRatio;
        cardView.Setup(card);
        return cardView;
    }

    private float GetHandScale()
    {
        return HandView.Instance != null ? HandView.Instance.HandCardScale : 0.55f;
    }
}
