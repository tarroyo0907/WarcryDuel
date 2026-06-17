using System.Collections;
using UnityEngine;
// Tyler Arroyo
// Move Executor
// Abstract base for all move execution logic
public abstract class MoveExecutor : ScriptableObject
{
    public abstract IEnumerator Execute(Figurine attacker, Figurine defender, int attackStat);
}
