using System.Collections;
using UnityEngine;
// Tyler Arroyo
// Move Effect
// Abstract base for all move effect logic
public abstract class MoveEffect : ScriptableObject
{
    public abstract IEnumerator Execute(Multiplayer_Player moveEffectPlayer, Multiplayer_Player attacker, Multiplayer_Player defender);
    public virtual void OnPlayerInteract(GameObject hitObject, Multiplayer_Player player) { }
}
