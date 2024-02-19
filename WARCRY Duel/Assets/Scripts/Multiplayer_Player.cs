using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System;
using Unity.Services.Matchmaker.Models;
using UnityEngine.Rendering.Universal;
using Unity.Burst.CompilerServices;
using System.Security.AccessControl;
using System.IO;
// Tyler Arroyo
// Multiplayer Player Class
// Manages the Player Class for Multiplayer Battles
public class Multiplayer_Player : NetworkBehaviour
{
    #region Delegates
    public delegate void PlayerHandler(Multiplayer_Player player);
    public delegate void FigurineHandler(Figurine figurine);
    public delegate void BattleHandler(Figurine attackerFigure, Figurine enemyFigure, ulong attackerID);
    #endregion

    #region Event Initialization
    public static event PlayerHandler PlayerSceneLoad;
    public static event PlayerHandler PrepareGame;
    public static event PlayerHandler SelectOwnFigurine;
    public static event PlayerHandler SelectEnemyFigurine;
    public static event PlayerHandler OnFigureMoved;
    public static event PlayerHandler TurnStart;
    public static event PlayerHandler OnEndTurn;
    public static event PlayerHandler OnBattleStart;
    public static NetworkSceneManager.OnLoadCompleteDelegateHandler sceneLoadedDelegate;
    public static event BattleHandler UpdateBattleFigures;
    public static event PlayerHandler FindingPossibleTargets;
    public static event PlayerHandler OnCompletedMoveEffect;
    public static event PlayerHandler OnCompletedExternalMove;
    #endregion

    #region Data Fields
    [Header("Player Team")]
    [SerializeField] private List<GameObject> playerTeamPrefabs = new List<GameObject>();
    [SerializeField] private List<NetworkObjectReference> playerTeamUnits = new List<NetworkObjectReference>();
    #endregion

    #region Gameplay Fields
    [SerializeField] private Figurine selectedFigurine;
    [SerializeField] public int playerID;
    [SerializeField] public bool isHighlightingPositions;
    [SerializeField] public string playerTeam;
    public Figurine playerBattleFigure;
    public Figurine enemyBattleFigure;

    public FigurineMove combatMove;
    public Dictionary<FigurineEffect.MoveEffects, int> moveEffects = new Dictionary<FigurineEffect.MoveEffects, int>();
    public string activeExternalMove;
    #endregion

    #region Properties
    public List<GameObject> PlayerTeamPrefabs { get { return playerTeamPrefabs; } }
    public List<NetworkObjectReference> PlayerTeamUnits { get { return playerTeamUnits; } set { playerTeamUnits = value; } }
    public Figurine SelectedFigurine { get { return selectedFigurine; } }
    public bool IsHighlightingPositions { get { return isHighlightingPositions; } set { isHighlightingPositions = value; } }
    public int MoveEffectCount { get { return moveEffects.Count; } }
    #endregion

    private void Awake()
    {
        UpdateBattleFigures += UpdateBattleFigure;

        PlayerTeamData playerTeamData = new PlayerTeamData
        {
            figurineNames = new string[playerTeamPrefabs.Count]
        };

        for (int i = 0; i < playerTeamPrefabs.Count; i++)
        {
            playerTeamData.figurineNames[i] = playerTeamPrefabs[i].name;
        }

        string json = JsonUtility.ToJson(playerTeamData);
        Debug.Log(json);
    }

    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(this);

        // Initialization
        playerID = (int)OwnerClientId;
        playerTeam = "Player " + playerID;

        // Runs Delegates for both players
        Multiplayer_GameManager.EndCombatEvent += UpdateToCombatEnding;
        Figurine.OnApplyMoveEffect += ApplyMoveEffect;
        PlayerUI.OnEndTurn += EndingTurn;
        OnCompletedMoveEffect += ClearMoveEffect;

        Debug.Log("Waiting for Scene to Change...");
        NetworkManager.SceneManager.OnLoadEventCompleted += GamePreparation;

        // Runs Delegates that only the owner of this player should run
        if (!IsOwner) { return; }

        
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) { return; }

        // Checks if player touches the screen or clicks the mouse
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            PlayerInteract();
        }

    }

    #region Player Interaction
    private void PlayerInteract()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Casts the ray and get the first game object hit
        Physics.Raycast(ray, out hit);

        if (hit.collider != null)
        {
            string hitReferenceName = hit.collider.gameObject.name;
            Debug.Log("Hit Object's Name: " + hitReferenceName);
            PlayerInteractServerRpc(hitReferenceName);
        }

    }

    /// <summary>
    /// Occurs whenever the player interacts with the screen
    /// </summary>
    [ServerRpc]
    public void PlayerInteractServerRpc(string hitName, ServerRpcParams serverRpcParams = default)
    {
        // Checks if it's the clients turn
        ulong playerID = serverRpcParams.Receive.SenderClientId;
        GameObject hit = GameObject.Find(hitName);

        // Checks if there is a current active move effect
        Multiplayer_GameManager.MoveEffectStateEnum currentMoveEffectState = Multiplayer_GameManager.Instance.MoveEffectState;
        if (currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERONE ||
            currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERTWO)
        {
            if (playerID == 1 && currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERONE)
            {
                CompleteMoveEffect(hit);
                return;
            }
            else if (playerID == 2 && currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERTWO)
            {
                CompleteMoveEffect(hit);
                return;
            }
            else
            {
                return;
            }
        }

        // Check if there is a current external move active
        if (activeExternalMove != "" && OwnerClientId == (ulong) Multiplayer_GameManager.Instance.GameBattleState)
        {
            CompleteExternalMove(hit);
            return;
        }

        if (OwnerClientId == (ulong)Multiplayer_GameManager.Instance.GameBattleState)
        {
            if (DetectAttackFigure(hit, serverRpcParams)) { return; }
            if (DetectMoveFigure(hit, serverRpcParams))
            {
                OnEndTurn?.Invoke(this);
                return;
            }
        }

        if (DetectSelectFigure(hit, serverRpcParams)) { return; }
    }

    public bool DetectMoveFigure(GameObject hit, ServerRpcParams serverRpcParams)
    {
        if (hit.tag == "BoardSpace")
        {
            if (Multiplayer_GameManager.Instance.MovementTurns >= 1)
            {
                if (selectedFigurine.Team != playerTeam)
                {
                    return false;
                }

                GameObject boardSpace = hit;
                Tile tile = boardSpace.GetComponent<Tile>();

                bool isPossibleSpace = false;
                foreach (List<Tile> tiles in selectedFigurine.PossiblePositions)
                {
                    if (tiles.Contains(tile))
                    {
                        isPossibleSpace = true;
                    }
                }

                if (isPossibleSpace)
                {
                    Debug.Log("IsPossibleSpace");
                    StartCoroutine(selectedFigurine.MovementSequence(tile));
                }
                else
                {
                    return false;
                }

                // Creates ClientRpcParams to identify which client to send the rpc to
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } }
                };

                // Decrements Movement Turns
                Multiplayer_GameManager.Instance.MovementTurns--;

                // Initiates MovedFigureEvent on Client
                UpdateFigurePositionClientRpc(selectedFigurine.name, tile.gameObject.name);
                MovedFigureCallbackClientRpc(clientRpcParams);
                return true;
            }
            
        }

        return false;
    }

    [ClientRpc]
    private void UpdateFigurePositionClientRpc(string figureName, string newFigureSpaceName)
    {
        Debug.Log("Updating Figurine Position!");
        Figurine figurine = GameObject.Find(figureName).GetComponent<Figurine>();
        figurine.CurrentSpacePos = GameObject.Find(newFigureSpaceName);
    }

    [ClientRpc]
    private void MovedFigureCallbackClientRpc(ClientRpcParams clientRpcParams)
    {
        Debug.Log("Moved Figure Callback Client Rpc!");

        // If the player can still attack, get the selected figures possible targets
        if (Multiplayer_GameManager.Instance.CanAttack)
        {
            selectedFigurine.GetPossibleTargets();
        }

        OnFigureMoved?.Invoke(this);
    }

    public bool DetectSelectFigure(GameObject hit, ServerRpcParams serverRpcParams)
    {
        // Detects if you click on one of your Figurines
        Debug.Log("Detecting Select Figure!");
        if (hit.tag == "Figurine")
        {
            selectedFigurine = hit.GetComponent<Figurine>();

            if (hit.GetComponent<Figurine>().Team == $"Player {playerID}")
            {
                Debug.Log("Clicked Own Figure!");

                try
                {
                    if (hit.GetInstanceID() == selectedFigurine.GetInstanceID()
                        || hit.GetComponent<Figurine>().CurrentSpacePos.name.Contains("Infirmary"))
                    {
                        return false;
                    }
                }
                catch (System.Exception)
                {
                    // Do Nothing
                }

                
                IsHighlightingPositions = false;

                if (OwnerClientId == (ulong)Multiplayer_GameManager.Instance.GameBattleState)
                {
                    // Checks if the player can still move this turn
                    if (Multiplayer_GameManager.Instance.MovementTurns >= 1)
                    {
                        IsHighlightingPositions = true;
                        selectedFigurine.GetPossiblePositions();
                        SetupBoard.Instance.FindAvailablePaths();
                    }

                    if (Multiplayer_GameManager.Instance.CanAttack)
                    {
                        selectedFigurine.GetPossibleTargets();
                        
                    }
                }

                // Creates ClientRpcParams to identify which client to send the rpc to
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } }
                };

                SelectOwnFigurineClientRpc(selectedFigurine.gameObject, IsHighlightingPositions, clientRpcParams);
                return true;
            }
            else
            {
                // When a player selects an enemy figurine
                Debug.Log("Selected enemy figurine!");

                // Creates ClientRpcParams to identify which client to send the rpc to
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } }
                };

                SelectEnemyFigurineClientRpc(selectedFigurine.gameObject, clientRpcParams);
            }
            
        }

        return false;
    }

    [ClientRpc]
    private void SelectEnemyFigurineClientRpc(NetworkObjectReference figurineNetworkObject, ClientRpcParams clientRpcParams = default)
    {
        // Grabs the figurine from the object reference and sets the player's selected figurine to the proper figurine
        figurineNetworkObject.TryGet(out NetworkObject figurineObject);
        Figurine figurine = figurineObject.gameObject.GetComponent<Figurine>();
        selectedFigurine = figurine;

        // Invokes Select Figurine Callback
        SelectEnemyFigurine?.Invoke(this);
    }

    public bool DetectAttackFigure(GameObject hit, ServerRpcParams serverRpcParams)
    {
        // Detects if you clicked on a figurine
        if (hit.tag == "Figurine")
        {
            Figurine enemyFigure = hit.GetComponent<Figurine>();
            // Checks if the figure is an enemy
            if (enemyFigure.Team != $"Player {playerID}")
            {
                // Checks if it is a possible target to your selected figurine
                if (selectedFigurine.PossibleTargets.Contains(enemyFigure.gameObject))
                {
                    // Starts Attack Sequence
                    Debug.Log("Starting Battle Attack Sequence");

                    // Sets both player's figures
                    Multiplayer_Player player1 = NetworkManager.Singleton.ConnectedClients[1].PlayerObject.GetComponent<Multiplayer_Player>();
                    Multiplayer_Player player2 = NetworkManager.Singleton.ConnectedClients[2].PlayerObject.GetComponent<Multiplayer_Player>();

                    if (playerID == 1)
                    {
                        player1.playerBattleFigure = selectedFigurine;
                        player1.enemyBattleFigure = enemyFigure;

                        player2.playerBattleFigure = enemyFigure;
                        player2.enemyBattleFigure = selectedFigurine;
                    }
                    else
                    {
                        player1.playerBattleFigure = enemyFigure;
                        player1.enemyBattleFigure = selectedFigurine;

                        player2.playerBattleFigure = selectedFigurine;
                        player2.enemyBattleFigure = enemyFigure;
                    }

                    OnBattleStart?.Invoke(this);
                    BattleStartClientRpc(playerBattleFigure.gameObject, enemyBattleFigure.gameObject, (ulong) playerID);
                    return true;
                }
            }
        }

        return false;
    }
    #endregion

    #region MoveEffects
    public void ApplyMoveEffect(Figurine sender, KeyValuePair<FigurineEffect.MoveEffects, int> moveEffect)
    {
        Debug.Log("Player Responding to ApplyMoveEffect Event!");
        // Only Apply Move Effect if the Figurine is part of the same team as the player
        if (sender.Team != playerTeam) { return; }

        // Add Move Effect
        Debug.Log("Applying Move Effect to Player!");
        moveEffects.Add(moveEffect.Key, moveEffect.Value);
    }

    public void CompleteMoveEffect(GameObject hitObject)
    {
        Debug.Log("Checking for a Completed Move Effect!");
        switch (Multiplayer_GameManager.Instance.activeMoveEffect)
        {
            case FigurineEffect.MoveEffects.Pushback:
                List<Tile>[] enemyPossiblePositions = enemyBattleFigure.GetPossiblePositions();
                foreach (Tile possiblePosition in enemyPossiblePositions[0])
                {
                    if (hitObject == possiblePosition.gameObject)
                    {
                        Debug.Log("Completed Pushback Move Effect!");
                        StartCoroutine(enemyBattleFigure.MoveFigure(enemyBattleFigure.CurrentSpacePos.GetComponent<Tile>(), possiblePosition, 1.5f));
                        CompleteMoveEffectClientRpc();
                        OnCompletedMoveEffect?.Invoke(this);
                        break;
                    }
                }
                break;
            default:
                break;
        }
    }

    [ClientRpc]
    private void CompleteMoveEffectClientRpc()
    {
        OnCompletedMoveEffect?.Invoke(this);
        Multiplayer_GameManager.Instance.activeMoveEffect = FigurineEffect.MoveEffects.None;
        
    }

    private void ClearMoveEffect(Multiplayer_Player player)
    {
        moveEffects.Remove(Multiplayer_GameManager.Instance.activeMoveEffect);
        Debug.Log("Move Effect Count : " + moveEffects.Count);
    }

    #endregion

    #region External Moves
    public void CompleteExternalMove(GameObject hitObject)
    {
        switch (activeExternalMove)
        {
            case "Fortification":
                List<Tile>[] possibleSpawnLocations = selectedFigurine.GetPossiblePositions();
                foreach(Tile possibleSpawnLoc in possibleSpawnLocations[0])
                {
                    if (hitObject == possibleSpawnLoc.gameObject)
                    {
                        Debug.Log("Completed Fortification External Move!");

                        // Spawns in the Boulder Wall
                        Vector3 spawnLoc = possibleSpawnLoc.transform.position += new Vector3(0, 5f, 0);
                        GameObject rockWallPrefab = Resources.Load<GameObject>("Spawnables/Rock_Spawnable");
                        GameObject prefabInstance = Instantiate(rockWallPrefab, spawnLoc, selectedFigurine.transform.rotation);
                        Figurine wallFigurine = prefabInstance.GetComponent<Figurine>();
                        wallFigurine.CurrentSpacePos = possibleSpawnLoc.gameObject;
                        wallFigurine.Team = selectedFigurine.Team;
                        prefabInstance.GetComponent<NetworkObject>().Spawn();

                        activeExternalMove = "";
                        OnCompletedExternalMove?.Invoke(this);
                        CompleteExternalMoveClientRpc();
                        break;
                    }
                }
                break;
            default:
                break;
        }
    }

    [ClientRpc]
    private void CompleteExternalMoveClientRpc()
    {
        activeExternalMove = "";
        OnCompletedExternalMove?.Invoke(this);
    }
    #endregion

    [ClientRpc]
    public void BattleStartClientRpc(NetworkObjectReference attackerFigurine,NetworkObjectReference enemyFigurine, ulong attackerID)
    {
        Debug.Log("Invoking Battle Start Client Rpc");

        attackerFigurine.TryGet(out NetworkObject attackerNetworkObject);
        Figurine attackerFigure = attackerNetworkObject.gameObject.GetComponent<Figurine>();

        enemyFigurine.TryGet(out NetworkObject enemyNetworkObject);
        Figurine enemyFigure = enemyNetworkObject.gameObject.GetComponent<Figurine>();

        UpdateBattleFigures(attackerFigure, enemyFigure, attackerID);
        OnBattleStart.Invoke(this);
    }

    private void UpdateBattleFigure(Figurine attackerFigurine, Figurine enemyFigurine, ulong attackerID)
    {
        Debug.Log("Attacker ID : " + attackerID);
        Debug.Log("Player ID : " + playerID);

        // You are attacking
        if (attackerID == (ulong) playerID)
        {
            playerBattleFigure = attackerFigurine;
            enemyBattleFigure = enemyFigurine;
        }
        // You are not the attacker
        else
        {
            playerBattleFigure = enemyFigurine;
            enemyBattleFigure = attackerFigurine;
        }

        if (playerBattleFigure.isAbleToFight == false)
        {
            combatMove = Resources.Load<FigurineMove>("Moves/Empty Move");
        }
    }

    [ClientRpc]
    private void SelectOwnFigurineClientRpc(NetworkObjectReference figurineNetworkObject, bool IsHighlightingPositions, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Selected Own Figurine Client Rpc");

        // Grabs the figurine from the object reference and sets the player's selected figurine to the proper figurine
        figurineNetworkObject.TryGet(out NetworkObject figurineObject);
        Figurine figurine = figurineObject.gameObject.GetComponent<Figurine>();
        selectedFigurine = figurine;
        this.IsHighlightingPositions = IsHighlightingPositions;

        IsHighlightingPositions = false;

        // Invokes Select Figurine Callback
        SelectOwnFigurine?.Invoke(this);

        // Checks if the player can still move this turn
        if (Multiplayer_GameManager.Instance.MovementTurns >= 1)
        {
            IsHighlightingPositions = true;
            selectedFigurine.GetPossiblePositions();
            SetupBoard.Instance.FindAvailablePaths();
        }

        if (Multiplayer_GameManager.Instance.CanAttack)
        {
            selectedFigurine.GetPossibleTargets();
            FindingPossibleTargets?.Invoke(this);
        }

    }

    #region Game Preparation
    /// <summary>
    /// Runs whenever the Multiplayer Battle Scene gets completely loaded in.
    /// Runs for only the clients player
    /// </summary>
    /// <param name="sceneEvent"></param>
    public void SceneLoaded(SceneEvent sceneEvent)
    {
        Debug.Log("Scene Loaded! : " + sceneEvent.ToString());
        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
        {
            PlayerSceneLoad.Invoke(this);
        }

    }

    /// <summary>
    /// Runs whenever the Multiplayer Battle Scene gets loaded in.
    /// Runs for the both player objects
    /// </summary>
    /// <param name="sceneEvent"></param>
    public void GamePreparation(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log("Preparing Game!");

        if (playerID == 1)
        {
            Multiplayer_GameManager.Instance.player1 = this;
        }
        else if (playerID == 2)
        {
            Multiplayer_GameManager.Instance.player2 = this;
        }

        if (!IsOwner) { return; }

        PrepareGame?.Invoke(this);
    }

    public void LoadPlayerTeam()
    {
        if (File.Exists(Application.dataPath + "/savedTeam.txt"))
        {
            string saveString = File.ReadAllText(Application.dataPath + "/savedTeam.txt");

            PlayerTeamData playerTeamData = JsonUtility.FromJson<PlayerTeamData>(saveString);
            for (int i = 0; i < playerTeamData.figurineNames.Length; i++)
            {
                Debug.Log("Figurine Name : " + playerTeamData.figurineNames[i]);
                playerTeamPrefabs[i] = Resources.Load<GameObject>("Figurines/" + playerTeamData.figurineNames[i]);
            }
        }
    }
    #endregion
    public void StartTurn(Multiplayer_GameManager gameManager)
    {
        TurnStart(this);
    }

    private void UpdateToCombatEnding()
    {
        combatMove = null;
    }

    private void EndingTurn()
    {
        activeExternalMove = "";
    }

    private class PlayerTeamData
    {
        public string[] figurineNames;
    }
}
