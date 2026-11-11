using UnityEngine;
[System.Serializable]
public class DrawCardsEffect : Effect
{
    [SerializeField] private int drawAmount;

    public override GameAction GetGameAction()
    {
        DrawCardsGA drawCardsGA = new DrawCardsGA(drawAmount);
        return drawCardsGA;
    }
}

