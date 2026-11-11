using UnityEngine;

public class DrawCardsGA : GameAction
{
    internal int amount;

    public int Amount { get; set; }
    public DrawCardsGA(int amount )
    {
        Amount = amount;
    }
}
