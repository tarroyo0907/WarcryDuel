using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/GrowingFlamesExecutor")]
public class GrowingFlamesExecutor : MoveExecutor
{
    public override IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat)
    {
        yield return new WaitForSeconds(0.1f);
    }
}
