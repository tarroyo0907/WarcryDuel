using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Earthquake")]
public class EarthquakeExecutor : MoveExecutor
{
    public MoveEffect pushbackEffect;

    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        defender.incomingEffect.IncomingDamage += (int)(attackStat * 1.5);
        defender.incomingEffect.moveEffects.Add(pushbackEffect);
        yield return new WaitForSeconds(0.1f);
        defender.TakeEffect();
    }
}
