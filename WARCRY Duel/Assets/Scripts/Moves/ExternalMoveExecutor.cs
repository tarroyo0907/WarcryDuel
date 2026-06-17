using UnityEngine;
// Tyler Arroyo
// External Move Executor
// Abstract base for all external move execution logic
public abstract class ExternalMoveExecutor : ScriptableObject
{
    // Runs server-side logic. Returns null if validation fails, otherwise returns params to sync to clients.
    public abstract string[] ExecuteOnServer(GameObject hitObject, Multiplayer_Player player, Figurine selectedFigurine);

    // Runs client-side sync using params returned from ExecuteOnServer.
    public abstract void ExecuteOnClient(string[] parameters);
}
