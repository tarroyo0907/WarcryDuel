using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
// Tyler Arroyo
// Section Manager
// Manages the bench/infirmary section of a team
public class SectionManager : NetworkBehaviour
{
    public delegate void InfirmaryDelegate();

    public static event InfirmaryDelegate OnSendUnitsToInfirmary;

    #region Data Fields
    // Data Fields
    [SerializeField] private List<Figurine> defeatedFigurines = new List<Figurine>();
    #endregion

    #region Base Methods
    // Start is called before the first frame update
    void Start()
    {
        Figurine.DefeatedEvent += FigureDefeated;
        Multiplayer_GameManager.EndCombatEvent += SendUnitsToInfirmary;
        Figurine.OnStopMoving += SendUnitsToInfirmary;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    #endregion

    private void FigureDefeated(Figurine figurine)
    {
        // Adds the defeated figurine to a list of defeated figurines this turn
        defeatedFigurines.Add(figurine);
    }

    private void SendUnitsToInfirmary(Figurine figurine)
    {
        SendUnitsToInfirmary();
    }

    private void SendUnitsToInfirmary()
    {
        Debug.Log("Send Units to Infirmary");
        OnSendUnitsToInfirmary?.Invoke();

        // Iterates through each defeated figurine to send them to the infirmary
        for (int i = 0; i < defeatedFigurines.Count; i++)
        {
            Figurine figurine = defeatedFigurines[i];
            if (figurine.isSpawnable)
            {
                figurine.GetComponent<NetworkObject>().Despawn();
                continue;
            }

            StartCoroutine(SendToInfirmary(figurine));
        }

        // Clears the list of defeated figures for this turn
        defeatedFigurines.Clear();
    }

    /// <summary>
    /// Transfers a figurine to its infirmary.
    /// </summary>
    /// <param name="figurine"> Figurine to send to Infirmary.</param>
    IEnumerator SendToInfirmary(Figurine figurine)
    {
        Debug.Log("Moving Unit to Informary");
        int FigureBenchCount = 8;
        figurine.currentHealth = figurine.totalHealth;
        figurine.healthBar.value = figurine.currentHealth;
        UpdateFigureHPClientRpc(figurine.figurineName);
        figurine.isDefeated = false;

        // Grabs the bench that the figure will be moved to
        GameObject figureBench = null;
        if (figurine.Team == "Player 1")
        {
            figureBench = GameObject.Find("Player 1 Bench");
        }
        else if (figurine.Team == "Player 2")
        {
            figureBench = GameObject.Find("Player 2 Bench");
        }

        Debug.Log("FIGURE BENCH : " + figureBench.name);

        // Grabs a list of all the figures on the same team
        GameObject[] tempPlayerFigures = GameObject.FindGameObjectsWithTag("Figurine");
        List<GameObject> playerFigures = new List<GameObject>();
        foreach (GameObject figure in tempPlayerFigures)
        {
            if (figure.GetComponent<Figurine>().Team == figurine.Team && figure != figurine)
            {
                playerFigures.Add(figure);
            }
        }

        // Iterates through each spot in the bench starting from the infirmary
        for (int i = FigureBenchCount - 1; i > 0 ; i--)
        {
            // Checks if there is a unit occupying the bench spot
            bool unitAtSection = false;
            foreach (GameObject figure in playerFigures)
            {
                if (figure.GetComponent<Figurine>().CurrentSpacePos == figureBench.transform.GetChild(i).gameObject)
                {
                    unitAtSection = true;
                    break;
                }
            }

            // If there is a unit at the section, iterate again
            if (unitAtSection)
            {
                continue;
            }

            // Once there is an empty spot found, goes in reverse starting at that spot
            for (int a = i + 1; a < FigureBenchCount; a++)
            {
                Figurine unitFigure = null;
                foreach (GameObject figure in playerFigures)
                {
                    if (figure.GetComponent<Figurine>().CurrentSpacePos == figureBench.transform.GetChild(a).gameObject)
                    {
                        unitFigure = figure.GetComponent<Figurine>();
                        break;
                    }
                }

                Tile _startingTile = unitFigure.CurrentSpacePos.GetComponent<Tile>();
                Tile _endingTile = figureBench.transform.GetChild(a - 1).gameObject.GetComponent<Tile>();
                StartCoroutine(unitFigure.MoveFigure(_startingTile, _endingTile, 3f));
                i++;

                // Waits before continuing
                yield return new WaitForSeconds(1f);
            }

            Tile startingTile = figurine.CurrentSpacePos.GetComponent<Tile>();
            Tile endingTile = figureBench.transform.GetChild(FigureBenchCount - 1).gameObject.GetComponent<Tile>();

            Debug.Log("ENDING TILE : " + endingTile.name);
            StartCoroutine(figurine.MoveFigure(startingTile, endingTile, 3f));
            break;
        }
        
    }

    [ClientRpc]
    public void UpdateFigureHPClientRpc(string figurineName)
    {
        GameObject selectedFigurine = GameObject.Find(figurineName);
        Figurine figurine = selectedFigurine.GetComponent<Figurine>();

        figurine.currentHealth = figurine.totalHealth;
        figurine.healthBar.value = figurine.currentHealth;
    }
}
