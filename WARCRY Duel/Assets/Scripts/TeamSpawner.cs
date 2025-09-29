using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Matchmaker.Models;
using static UnityEngine.Rendering.DebugUI;
using UnityEngine.UI;

// Tyler Arroyo
// Team Spawner
// Spawns all of the player's units
public class TeamSpawner : NetworkBehaviour
{
    // Delegates
    public delegate void FiguresSpawnHandler();

    // Events
    public static event FiguresSpawnHandler OnFiguresSpawned;

    // Fields
    [SerializeField] private List<GameObject> playerTeamPrefabs = new List<GameObject>();
    [SerializeField] private Transform spawnTransform;

    [SerializeField] private Transform player1Bench;
    [SerializeField] private Transform player2Bench;
    [SerializeField] private Multiplayer_Player player;

    private void Awake()
    {
        
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Is Owner : " + IsOwner);

        if (!IsOwner)
        {
            return;
        }

        Multiplayer_Player.PrepareGame += LayoutFigurines;
        spawnTransform = null;
        player = this.gameObject.GetComponent<Multiplayer_Player>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Spawns in all the player's figurines at a bench.
    /// </summary>
    /// <param name="player">Player's Event that triggered this method</param>
    public void LayoutFigurines (Multiplayer_Player player)
    {
        Debug.Log("Spawning Figurines!");
        playerTeamPrefabs = player.PlayerTeamPrefabs;

        Debug.Log("Spawning Figurines from Server RPC");
        try
        {
            SpawnNetworkFigurineServerRpc();
        }
        catch (System.Exception)
        {

        }

        Debug.Log("Subscribing to OnListChanged Delegate");
        //player.PlayerTeamUnits.OnListChanged += OnFigureSpawnedCallback;
    }

    private void OnFigureSpawnedCallback(NetworkListEvent<NetworkObjectReference> changeEvent)
    {
        Debug.Log("OnFigureSpawnedCallback");
        if (player.PlayerTeamUnits.Count == 6)
        {
            OnFiguresSpawned?.Invoke();
        }
    }

    [ServerRpc]
    public void SpawnNetworkFigurineServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // Figures out which bench to spawn the figurines at
        switch (player.OwnerClientId)
        {
            case 1:
                spawnTransform = GameObject.Find(player1Bench.name).transform;
                break;

            case 2:
                spawnTransform = GameObject.Find(player2Bench.name).transform;
                break;

            default:
                break;
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { player.OwnerClientId }
            }
        };

        // Starts to Spawn Figurines
        Debug.Log("Player Team Count : " + player.PlayerTeamPrefabs.Count);
        int figurineNumber = 0;
        foreach (GameObject figurine in player.PlayerTeamPrefabs)
        {
            // Finds the exact bench spot to spawn Figurine at
            figurineNumber++;
            string benchSpaceName = "Bench Slot " + figurineNumber;
            GameObject benchSpot = spawnTransform.Find(benchSpaceName).gameObject;
            Vector3 benchSpotPos = benchSpot.transform.position;

            //Spawn Figurine
            Debug.Log("Spawn Network Figurine");
            GameObject benchSpawn = GameObject.Find(spawnTransform.name);
            GameObject spawnedFigurine = Instantiate(figurine, benchSpotPos, figurine.transform.rotation, benchSpawn.transform);
            NetworkObject networkObject = spawnedFigurine.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                Debug.Log($"Trying to Spawn Network Object for {networkObject.name}");
                networkObject.Spawn();
            }

            spawnedFigurine.transform.Rotate(new Vector3(0, 1, 0), -90);
            string teamName = "Player " + player.OwnerClientId;
            spawnedFigurine.name = teamName + " - " + spawnedFigurine.name + " " + figurineNumber;
            spawnedFigurine.GetComponent<Figurine>().CurrentSpacePos = benchSpot;
            spawnedFigurine.tag = "Figurine";
            spawnedFigurine.GetComponent<Figurine>().Team = teamName;

            // Rotate Figurine 180 degrees if they are player 2
            if (player.OwnerClientId == 2)
            {
                spawnedFigurine.transform.Rotate(new Vector3(0, 1, 0), 180);
            }
            spawnedFigurine.GetComponent<Figurine>().StoreOriginalAlignment();
            player.PlayerTeamUnits.Add(spawnedFigurine);
            UpdatePlayerUnitsClientRpc(spawnedFigurine, spawnedFigurine.name, benchSpot.name, teamName, player.OwnerClientId);
        }

        Debug.Log("Completed Server RPC");
        FiguresSpawnedClientRpc(clientRpcParams);
    }

    [ClientRpc]
    private void FiguresSpawnedClientRpc(ClientRpcParams clientRpcParams)
    {
        OnFiguresSpawned?.Invoke();
    }

    [ClientRpc]
    private void UpdatePlayerUnitsClientRpc(NetworkObjectReference figurine, string figurineName, string benchSpotName, string teamName, ulong playerID)
    {
        Debug.Log("Adding Figurine to Player List");
        figurine.TryGet(out NetworkObject figurineObject);
        Figurine figure = figurineObject.gameObject.GetComponent<Figurine>();
        figure.name = figurineName;
        figure.Team = teamName;
        figure.CurrentSpacePos = GameObject.Find(benchSpotName);
        figure.currentHealth = figure.totalHealth;
        figurineObject.gameObject.tag = "Figurine";

        if (NetworkManager.Singleton.LocalClientId == playerID)
        {
            player.PlayerTeamUnits.Add(figurine);
        }
        else
        {
            Transform FigurineUI = figurineObject.transform.Find("Figurine UI");
            FigurineUI.Rotate(new Vector3(0, 180, 0));
            FigurineUI.Find("MovementPoints").GetComponent<Image>().color = Color.red;
        }
        
    }
}
