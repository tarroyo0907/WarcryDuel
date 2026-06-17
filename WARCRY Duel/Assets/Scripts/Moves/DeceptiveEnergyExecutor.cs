using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Deceptive Energy")]
public class DeceptiveEnergyExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        int lifestealStacks = attacker.buffs[FigurineEffect.StatusEffects.Lifesteal];
        defender.incomingEffect.IncomingDamage += (int)(attackStat * 1.0) * lifestealStacks;
        attacker.incomingEffect.SelfBuffsToRemove.Add(FigurineEffect.StatusEffects.Lifesteal, lifestealStacks);
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
