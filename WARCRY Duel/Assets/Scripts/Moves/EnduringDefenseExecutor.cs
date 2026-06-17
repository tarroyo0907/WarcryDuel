using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Enduring Defense")]
public class EnduringDefenseExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        attacker.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.DefenseUp, 3);
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
