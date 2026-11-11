using System.Collections;
using UnityEngine;

public class DamageSystem : MonoBehaviour
{
    [SerializeField] private GameObject playerHitVFX;  // 玩家打敌人特效
    [SerializeField] private GameObject enemyHitVFX;  // 敌人打玩家特效

    void OnEnable()
    {
        ActionSystem.AttachPerformer<DealDamageGA>(DealDamagePerformer);
    }

    void OnDisable()
    {
        ActionSystem.DetachPerformer<DealDamageGA>();
    }

    private IEnumerator DealDamagePerformer(DealDamageGA dealDamageGA)
    {
        foreach (var target in dealDamageGA.Targets)
        {
            target.Damage(dealDamageGA.Amount);

            Vector3 hitPos;
            GameObject vfxToSpawn;

            if (dealDamageGA.IsEnemyAttack)
            {
                // 敌人打玩家：调整这里的数字，修正位置
                hitPos = target.transform.position + new Vector3(-1.2f, 0f, 0f);
                vfxToSpawn = enemyHitVFX;
            }
            else
            {
                // 玩家打敌人
                hitPos = target.transform.position + new Vector3(1f, 0f, 0f);
                vfxToSpawn = playerHitVFX;
            }

            Instantiate(vfxToSpawn, hitPos, Quaternion.identity);
            yield return new WaitForSeconds(0.15f);
        }
    }
}
