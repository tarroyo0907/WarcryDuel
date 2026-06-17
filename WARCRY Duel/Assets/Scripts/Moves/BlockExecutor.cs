using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Block")]
public class BlockExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        attacker.incomingEffect.BlockIncomingDamage = true;
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
