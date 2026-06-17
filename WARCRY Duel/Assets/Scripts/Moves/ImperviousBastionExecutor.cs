using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Impervious Bastion")]
public class ImperviousBastionExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        attacker.incomingEffect.BlockIncomingDamage = true;
        attacker.incomingEffect.RemoveAllDebuffs = false;
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
