using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/GrowingFlamesExecutor")]
public class GrowingFlamesExecutor : MoveExecutor
{
    public MoveEffect growingFlamesEffect;
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        defender.incomingEffect.IncomingDamage += (int)(attackStat * 1.0);
        defender.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Burn, 2);
        defender.incomingEffect.moveEffects.Add(growingFlamesEffect);
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
