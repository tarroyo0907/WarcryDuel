using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Slash")]
public class SlashExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        defender.incomingEffect.IncomingDamage += (int)(attackStat * 1.0);
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
