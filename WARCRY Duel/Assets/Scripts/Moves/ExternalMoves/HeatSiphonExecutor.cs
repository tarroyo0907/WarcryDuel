using UnityEngine;
[CreateAssetMenu(menuName = "Moves/Executors/HeatSiphonExecutor")]
public class HeatSiphonExecutor : ExternalMoveExecutor
{
    // Runs server-side logic. Returns null if validation fails, otherwise returns params to sync to clients.
    public override string[] ExecuteOnServer(GameObject hitObject, Multiplayer_Player player, Figurine selectedFigurine)
    {
        // Grabs all the figurines on the board
        GameObject[] Figurines = GameObject.FindGameObjectsWithTag("Figurine");

        foreach (GameObject figure in Figurines)
        {
            Figurine figurine = figure.GetComponent<Figurine>();
            // Check if the figurine has burn
            if (figurine.debuffs.ContainsKey(FigurineEffect.StatusEffects.Burn))
            {
                // Remove burn from the figurine
                int burnStacks = figurine.debuffs[FigurineEffect.StatusEffects.Burn];
                figurine.debuffs.Remove(FigurineEffect.StatusEffects.Burn);

                // Heal the selected figurine based on the number of burn stacks removed
                selectedFigurine.currentHealth += 3;
                selectedFigurine.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.AttackUp, 1);
                if (selectedFigurine.currentHealth > selectedFigurine.totalHealth)
                    selectedFigurine.currentHealth = selectedFigurine.totalHealth;
                
            }
        }
        selectedFigurine.TakeEffect();

        return new string[] { selectedFigurine.name };
    }

    // Runs client-side sync using params returned from ExecuteOnServer.
    public override void ExecuteOnClient(string[] parameters)
    {
        Figurine selectedFigurine = GameObject.Find(parameters[0]).GetComponent<Figurine>();

        // Grabs all the figurines on the board
        GameObject[] Figurines = GameObject.FindGameObjectsWithTag("Figurine");

        foreach (GameObject figure in Figurines)
        {
            Figurine figurine = figure.GetComponent<Figurine>();
            // Check if the figurine has burn
            if (figurine.debuffs.ContainsKey(FigurineEffect.StatusEffects.Burn))
            {
                // Remove burn from the figurine
                int burnStacks = figurine.debuffs[FigurineEffect.StatusEffects.Burn];
                figurine.debuffs.Remove(FigurineEffect.StatusEffects.Burn);

                // Heal the selected figurine based on the number of burn stacks removed
                selectedFigurine.currentHealth += 3;
                selectedFigurine.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.AttackUp, 1);
                if (selectedFigurine.currentHealth > selectedFigurine.totalHealth)
                    selectedFigurine.currentHealth = selectedFigurine.totalHealth;
                selectedFigurine.TakeEffect();
            }
        }
    }
}
