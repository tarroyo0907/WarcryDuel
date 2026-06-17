using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Moves/Executors/Rejuvinate")]
public class RejuvinateExecutor : ExternalMoveExecutor
{
    public override string[] ExecuteOnServer(GameObject hitObject, Multiplayer_Player player, Figurine selectedFigurine)
    {
        if (hitObject.tag != "Figurine") return null;

        Figurine clickedFigurine = hitObject.GetComponent<Figurine>();
        if (clickedFigurine.Team != $"Player {player.playerID}") return null;

        try { if (clickedFigurine.CurrentSpacePos.name.Contains("Infirmary")) return null; }
        catch (System.Exception) { }

        FigurineEffect.StatusEffects statusEffect = FigurineEffect.StatusEffects.AttackUp;
        clickedFigurine.buffs[statusEffect] = clickedFigurine.buffs.GetValueOrDefault(statusEffect) + 2;
        clickedFigurine.TakeEffect();

        return new string[] { clickedFigurine.name, statusEffect.ToString(), "2" };
    }

    public override void ExecuteOnClient(string[] parameters)
    {
        Figurine clickedFigurine = GameObject.Find(parameters[0]).GetComponent<Figurine>();
        FigurineEffect.StatusEffects statusEffect = (FigurineEffect.StatusEffects)Enum.Parse(typeof(FigurineEffect.StatusEffects), parameters[1]);
        int stacks = int.Parse(parameters[2]);
        clickedFigurine.buffs[statusEffect] = clickedFigurine.buffs.GetValueOrDefault(statusEffect) + stacks;
        clickedFigurine.TakeEffect();
    }
}
