using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Arcane Blast")]
public class ArcaneBlastExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        defender.incomingEffect.IncomingDamage += (int)(attackStat * 1.0);

        if (attacker.ability.abilityName == "Lifesteal")
        {
            attacker.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.Lifesteal, 1);
        }
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
