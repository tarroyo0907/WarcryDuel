using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Toxic Bud")]
public class ToxicBudExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        defender.incomingEffect.IncomingDamage += (int)(attackStat * 1.0);
        if (UnityEngine.Random.Range(0, 1) == 0)
        {
            defender.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.DefenseDown, 1);
        }
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
