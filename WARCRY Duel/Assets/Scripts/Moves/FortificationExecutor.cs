using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Fortification")]
public class FortificationExecutor : ExternalMoveExecutor
{
    public override string[] ExecuteOnServer(GameObject hitObject, Multiplayer_Player player, Figurine selectedFigurine)
    {
        if (hitObject.tag != "BoardSpace") return null;

        List<Tile>[] possibleSpawnLocations = selectedFigurine.GetPossiblePositions();
        foreach (Tile possibleSpawnLoc in possibleSpawnLocations[0])
        {
            if (hitObject != possibleSpawnLoc.gameObject) continue;

            GameObject rockWallPrefab = Resources.Load<GameObject>("Spawnables/Rock_Spawnable");
            GameObject oldRockWall = GameObject.Find(selectedFigurine.Team + " - " + "Rock_Spawnable(Clone)");
            if (oldRockWall != null) Object.Destroy(oldRockWall);

            Vector3 spawnLoc = possibleSpawnLoc.transform.position + new Vector3(0, 5f, 0);
            GameObject prefabInstance = Object.Instantiate(rockWallPrefab, spawnLoc, selectedFigurine.transform.rotation);
            Figurine wallFigurine = prefabInstance.GetComponent<Figurine>();
            string originalName = wallFigurine.name;
            wallFigurine.CurrentSpacePos = possibleSpawnLoc.gameObject;
            wallFigurine.Team = selectedFigurine.Team;
            prefabInstance.GetComponent<NetworkObject>().Spawn();
            wallFigurine.name = wallFigurine.Team + " - " + wallFigurine.name;
            wallFigurine.debuffs.Add(FigurineEffect.StatusEffects.Decay, 3);
            wallFigurine.CheckForSurroundKill();
            return new string[] { originalName, wallFigurine.name };
        }
        return null;
    }

    public override void ExecuteOnClient(string[] parameters)
    {
        GameObject rockWallGO = GameObject.Find(parameters[0]);
        rockWallGO.name = parameters[1];
        Figurine wallFigurine = rockWallGO.GetComponent<Figurine>();
        wallFigurine.debuffs.Add(FigurineEffect.StatusEffects.Decay, 3);
    }
}
