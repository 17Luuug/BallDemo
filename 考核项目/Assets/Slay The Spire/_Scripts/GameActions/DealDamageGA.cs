using System.Collections.Generic;

public class DealDamageGA : GameAction
{
    public int Amount { get; private set; }
    public List<CombatantView> Targets { get; private set; }
    // 新增标记：true=敌人发起的攻击(打玩家)，false=玩家卡牌发起的攻击(打敌人)
    public bool IsEnemyAttack { get; private set; }

    public DealDamageGA(int amount, List<CombatantView> targets, bool isEnemyAttack = false)
    {
        Amount = amount;
        Targets = targets;
        IsEnemyAttack = isEnemyAttack;
    }
}
