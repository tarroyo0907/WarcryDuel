using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Empowered Strike")]
public class EmpoweredStrikeExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        defender.incomingEffect.IncomingDamage += (int)(attackStat * 3.0);
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
