using System;
using System.Linq;
using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Cleansing Rose")]
public class CleansingRoseExecutor : ExternalMoveExecutor
{
    public override string[] ExecuteOnServer(GameObject hitObject, Multiplayer_Player player, Figurine selectedFigurine)
    {
        if (hitObject.tag != "Figurine") return null;

        Figurine clickedFigurine = hitObject.GetComponent<Figurine>();
        if (clickedFigurine.Team != $"Player {player.playerID}") return null;

        try { if (clickedFigurine.CurrentSpacePos.name.Contains("Infirmary")) return null; }
        catch (System.Exception) { }

        if (clickedFigurine.debuffs.Count == 0) return null;

        int randomDebuffIndex = UnityEngine.Random.Range(0, clickedFigurine.debuffs.Count);
        FigurineEffect.StatusEffects randomDebuff = clickedFigurine.debuffs.ElementAt(randomDebuffIndex).Key;
        clickedFigurine.debuffs.Remove(randomDebuff);

        return new string[] { clickedFigurine.name, randomDebuff.ToString() };
    }

    public override void ExecuteOnClient(string[] parameters)
    {
        Figurine clickedFigurine = GameObject.Find(parameters[0]).GetComponent<Figurine>();
        FigurineEffect.StatusEffects statusEffect = (FigurineEffect.StatusEffects)Enum.Parse(typeof(FigurineEffect.StatusEffects), parameters[1]);
        clickedFigurine.debuffs.Remove(statusEffect);
        clickedFigurine.TakeEffect();
    }
}
