using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Shuriken Throw")]
public class ShurikenThrowExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        for (int i = 0; i < 3; i++)
        {
            defender.incomingEffect.IncomingDamage += (int)(attackStat * 0.5);
            defender.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Bleed, 1);
            yield return new WaitForSeconds(0.25f);
            defender.TakeEffect();
        }
    }
}
