using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Smokebomb")]
public class SmokebombExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        attacker.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.Stealth, 2);
        attacker.incomingEffect.BlockIncomingDamage = true;
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
