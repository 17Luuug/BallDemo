using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DealDamageEffect : Effect
{
    [SerializeField] private int damageAmount;

    public override GameAction GetGameAction()
    {
        List<CombatantView> targets = new List<CombatantView>();
      
        targets.AddRange(EnemyBoardView.Instance.EnemyViews);

        DealDamageGA dealDamageGA = new DealDamageGA(damageAmount, targets, false);
        return dealDamageGA;
    }
}