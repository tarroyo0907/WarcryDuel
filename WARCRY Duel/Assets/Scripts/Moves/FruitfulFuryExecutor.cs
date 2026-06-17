using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/Fruitful Fury")]
public class FruitfulFuryExecutor : ExternalMoveExecutor
{
    public override string[] ExecuteOnServer(GameObject hitObject, Multiplayer_Player player, Figurine selectedFigurine)
    {
        if (hitObject.tag != "Figurine") return null;

        Figurine clickedFigurine = hitObject.GetComponent<Figurine>();
        if (clickedFigurine.Team != $"Player {player.playerID}") return null;

        try { if (clickedFigurine.CurrentSpacePos.name.Contains("Infirmary")) return null; }
        catch (System.Exception) { }

        if (!selectedFigurine.buffs.ContainsKey(FigurineEffect.StatusEffects.Lifesteal)) return null;

        int lifestealValue = selectedFigurine.buffs[FigurineEffect.StatusEffects.Lifesteal];
        clickedFigurine.currentHealth += lifestealValue * selectedFigurine.attackStat;
        if (clickedFigurine.currentHealth > clickedFigurine.totalHealth)
            clickedFigurine.currentHealth = clickedFigurine.totalHealth;
        clickedFigurine.TakeEffect();
        selectedFigurine.buffs.Remove(FigurineEffect.StatusEffects.Lifesteal);

        return new string[] { selectedFigurine.name, clickedFigurine.name };
    }

    public override void ExecuteOnClient(string[] parameters)
    {
        Figurine externalMoveSelectedFigurine = GameObject.Find(parameters[0]).GetComponent<Figurine>();
        Figurine clickedFigurine = GameObject.Find(parameters[1]).GetComponent<Figurine>();
        int lifestealValue = externalMoveSelectedFigurine.buffs[FigurineEffect.StatusEffects.Lifesteal];
        clickedFigurine.currentHealth += lifestealValue * externalMoveSelectedFigurine.attackStat;
        if (clickedFigurine.currentHealth > clickedFigurine.totalHealth)
            clickedFigurine.currentHealth = clickedFigurine.totalHealth;
        clickedFigurine.TakeEffect();
        externalMoveSelectedFigurine.buffs.Remove(FigurineEffect.StatusEffects.Lifesteal);
    }
}
