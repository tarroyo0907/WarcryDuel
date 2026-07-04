using System.Collections;
using System.Collections.Generic;

using Unity.Netcode;

using UnityEngine;
[CreateAssetMenu(menuName = "MoveEffects/GrowingFlamesEffect")]
public class GrowingFlamesEffect : MoveEffect
{
    public override IEnumerator Execute(Multiplayer_Player moveEffectPlayer, Multiplayer_Player attacker, Multiplayer_Player defender)
    {
        [ClientRpc]
        void GrowingFlamesEffectClientRpc(string figurines)
        {
            Debug.Log("Growing Flames Effect Client RPC called for figurines: " + figurines);
            string[] figurineNames = figurines.Split(',');
            foreach (string figurineName in figurineNames)
            {
                GameObject figurineGO = GameObject.Find(figurineName);
                Figurine figurine = figurineGO.GetComponent<Figurine>();
                figurine.incomingEffect.IncomingDamage += (int)(attacker.playerBattleFigure.attackStat * 2.0f);
                figurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Burn, 2);
                figurine.TakeEffect();
            }
        }

        List<GameObject> adjacentFigurines = new List<GameObject>();

        // Grabs the Tile Component of this figurine's current space
        Tile currentTile = null;
        try
        {
            currentTile = moveEffectPlayer.playerBattleFigure.CurrentSpacePos.GetComponent<Tile>();
        }
        catch (System.Exception)
        {
            Debug.Log($"Couldn't grab the Tile Component of {moveEffectPlayer.playerBattleFigure.CurrentSpacePos.name}");
        }

        // Grabs all the figurines on the board
        GameObject[] Figurines = GameObject.FindGameObjectsWithTag("Figurine");
        Debug.Log("Attacking Figure = " + attacker.playerBattleFigure.name);

        // Checks each accessible tile connecting to the parent tile
        foreach (Tile possibleTargetTile in currentTile.AccessibleTiles)
        {
            foreach (GameObject figure in Figurines)
            {
                if (figure.name != attacker.playerBattleFigure.gameObject.name)
                {
                    if (figure.GetComponent<Figurine>().CurrentSpacePos.name == possibleTargetTile.name)
                    {
                        if (figure.GetComponent<Figurine>().isDefeated == false)
                        {
                            // Adds figure as a possible target
                            adjacentFigurines.Add(figure);
                            Debug.Log($"Added {figure.name} as a possible target for Growing Flames");
                        }
                    }
                }
                
            }
        }

        yield return new WaitForSeconds(0.1f);

        // If there are any adjacent figurines, apply damage to them
        if (adjacentFigurines.Count > 0)
        {
            foreach (GameObject figure in adjacentFigurines)
            {
                // Apply damage to the adjacent figurine
                Figurine figurine = figure.GetComponent<Figurine>();
                figurine.incomingEffect.IncomingDamage += (int)(attacker.playerBattleFigure.attackStat * 2.0f);
                figurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Burn, 2);
                figurine.TakeEffect();
            }
        }

        string figurineNames = string.Join(",", adjacentFigurines.ConvertAll(f => f.name).ToArray());
        Debug.Log($"Sending Growing Flames Effect to clients for figurines: {figurineNames}");
        GrowingFlamesEffectClientRpc(figurineNames);
    }
}
