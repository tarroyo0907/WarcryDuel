using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Unity.Netcode;

using UnityEngine;

using static Multiplayer_GameManager;
// Tyler Arroyo
// Multiplayer Player Class
// Manages the Player Class for Multiplayer Battles
public class Multiplayer_Player : NetworkBehaviour
{
    #region Delegates
    public delegate void DefaultHandler();
    public delegate void PlayerHandler(Multiplayer_Player player);
    public delegate void FigurineHandler(Figurine figurine);
    public delegate void BattleHandler(Figurine attackerFigure, Figurine enemyFigure, ulong attackerID);
    public delegate void AnnouncementHandler(string announcement);
    #endregion

    #region Event Initialization
    public static event PlayerHandler PlayerSceneLoad;
    public static event PlayerHandler PrepareGame;
    public static event PlayerHandler SelectOwnFigurine;
    public static event PlayerHandler SelectEnemyFigurine;
    public static event PlayerHandler OnFigureMoved;
    public static event PlayerHandler OnFigureStartMoving;
    public static event PlayerHandler OnFigureStopMoving;
    public static event PlayerHandler TurnStart;
    public static event PlayerHandler OnEndTurn;
    public static event PlayerHandler OnBattleStart;
    public static NetworkSceneManager.OnLoadCompleteDelegateHandler sceneLoadedDelegate;
    public static event BattleHandler UpdateBattleFigures;
    public static event PlayerHandler FindingPossibleTargets;
    public static event PlayerHandler OnCompletedMoveEffect;
    public static event PlayerHandler OnCompletedExternalMove;
    public static event PlayerHandler OnPartyLoaded;
    public static event AnnouncementHandler ExternalMoveAnnouncement;
    #endregion

    #region Data Fields
    [Header("Player Team")]
    [SerializeField] private List<GameObject> playerTeamPrefabs = new List<GameObject>();
    [SerializeField] private List<NetworkObjectReference> playerTeamUnits = new List<NetworkObjectReference>();
    #endregion

    #region Gameplay Fields
    [SerializeField] public Figurine selectedFigurine;
    [SerializeField] public int playerID;
    [SerializeField] public bool isHighlightingPositions;
    [SerializeField] public string playerTeam;
    public Figurine playerBattleFigure;
    public Figurine enemyBattleFigure;
    public bool canInteract = true;
    private bool hasFigurineStartedMoving = false;

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

    #region Base Methods
    private void Awake()
    {
        UpdateBattleFigures += UpdateBattleFigure;

        
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
        Multiplayer_GameManager.OnCancelledMoveEffect += CancelMoveEffect;

        Debug.Log("Waiting for Scene to Change...");
        //NetworkManager.SceneManager.OnLoadEventCompleted += GamePreparation;
        Multiplayer_GameManager.OnChangeTurn += GamePreparation;
        Multiplayer_GameManager.OnChangeTurn += ClearSelectedFigurine;
        Multiplayer_GameManager.LoadPlayerTeams += LoadPlayerTeams;

        // Runs Delegates that only the owner of this player should run
        if (!IsOwner) { return; }

        
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner) { return; }

        // Checks if player touches the screen or clicks the mouse
        if ((Input.GetMouseButtonDown(0) || Input.touchCount > 0) && canInteract)
        {
            PlayerInteract();
        }

    }
    #endregion

    #region Player Interaction
    private void PlayerInteract()
    {
        try
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Casts the ray and get the first game object hit
            Physics.Raycast(ray, out hit);

            if (hit.collider != null)
            {
                canInteract = false;
                string hitReferenceName = hit.collider.gameObject.name;
                PlayerInteractServerRpc(hitReferenceName);
            }
        }
        catch (Exception)
        {
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
        if (hit == null)
        {
            return;
        }

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { playerID }
            }
        };

        // Checks if there is a current active move effect
        Multiplayer_GameManager.MoveEffectStateEnum currentMoveEffectState = Multiplayer_GameManager.Instance.MoveEffectState;
        if (currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERONE ||
            currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERTWO)
        {
            if (playerID == 1 && currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERONE)
            {
                CompleteMoveEffect(hit);
                OnPlayerInteractCompleteClientRpc(hit.name, clientRpcParams);
                return;
            }
            else if (playerID == 2 && currentMoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERTWO)
            {
                CompleteMoveEffect(hit);
                OnPlayerInteractCompleteClientRpc(hit.name, clientRpcParams);
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
            OnPlayerInteractCompleteClientRpc(hit.name, clientRpcParams);
            return;
        }

        // Checks if its the player's turn
        if (OwnerClientId == (ulong) Multiplayer_GameManager.Instance.GameBattleState)
        {
            if (DetectAttackFigure(hit, serverRpcParams)) { OnPlayerInteractCompleteClientRpc(hit.name, clientRpcParams); return; }
            if (DetectMoveFigure(hit, serverRpcParams))
            {
                OnEndTurn?.Invoke(this);
                OnPlayerInteractCompleteClientRpc(hit.name, clientRpcParams);
                return;
            }
        }

        if (DetectSelectFigure(hit, serverRpcParams)) { OnPlayerInteractCompleteClientRpc(hit.name, clientRpcParams); return; }
        OnPlayerInteractCompleteClientRpc(hit.name, clientRpcParams);
    }

    [ClientRpc]
    private void OnPlayerInteractCompleteClientRpc(string hitName, ClientRpcParams clientRpcParams = default)
    {
        GameObject hitObject = GameObject.Find(hitName);
        canInteract = true;

        if (hitObject.tag == "Figurine")
        {
            selectedFigurine = hitObject.GetComponent<Figurine>();
        }
    }

    public bool DetectMoveFigure(GameObject hit, ServerRpcParams serverRpcParams)
    {
        if(hit == null || selectedFigurine == null)
        {
            return false;
        }

        if (hit.tag == "BoardSpace")
        {
            if (Multiplayer_GameManager.Instance.MovementTurns >= 1)
            {
                try
                {
                    if (selectedFigurine.Team != playerTeam || selectedFigurine.debuffs.ContainsKey(FigurineEffect.StatusEffects.Wait))
                    {
                        return false;
                    }
                }
                catch (Exception e) { }

                // Creates ClientRpcParams to identify which client to send the rpc to
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } }
                };

                GameObject boardSpace = hit;
                Tile tile = boardSpace.GetComponent<Tile>();

                bool isPossibleSpace = false;
                if (selectedFigurine.PossiblePositions != null)
                {
                    foreach (List<Tile> tiles in selectedFigurine.PossiblePositions)
                    {
                        if (tiles.Contains(tile))
                        {
                            isPossibleSpace = true;
                        }
                    }
                }
                

                if (isPossibleSpace)
                {
                    StartCoroutine(StartMovingFigurine(clientRpcParams, tile));

                    
                    return true;
                }
                else
                {
                    return false;
                }
            }
            
        }

        return false;
    }

    [ClientRpc]
    private void UpdateFigurePositionClientRpc(string figureName, string newFigureSpaceName)
    {
        try
        {
            Figurine figurine = GameObject.Find(figureName).GetComponent<Figurine>();
            figurine.CurrentSpacePos = GameObject.Find(newFigureSpaceName);
        }
        catch (Exception)
        {
        }
        
    }
    [ClientRpc]
    private void StartedMovingFigureClientRpc(ClientRpcParams clientRpcParams)
    {
        OnFigureStartMoving?.Invoke(this);

        AcknowledgeMovementServerRpc();
    }

    private IEnumerator StartMovingFigurine(ClientRpcParams clientRpcParams, Tile tile)
    {
        hasFigurineStartedMoving = false;
        bool hasFigurineStoppedMoving = false;
        Multiplayer_GameManager.Instance.MovementTurns--;

        // Handler for the OnStopMoving event
        Figurine.FigurineHandler handler = null;
        handler = (figure) =>
        {
            if (figure == selectedFigurine)
            {
                hasFigurineStoppedMoving = true;
                Figurine.OnStopMoving -= handler; 
            }
        };
        Figurine.OnStopMoving += handler;

        StartedMovingFigureClientRpc(clientRpcParams);
        UpdateFigurePositionClientRpc(selectedFigurine.name, tile.gameObject.name);

        yield return new WaitUntil(() => hasFigurineStartedMoving);

        hasFigurineStartedMoving = false;
        
        StartCoroutine(selectedFigurine.MovementSequence(tile));

        yield return new WaitUntil(() => hasFigurineStoppedMoving);

        // update state
        MovedFigureCallbackClientRpc(clientRpcParams);

    }

    [ServerRpc]
    private void AcknowledgeMovementServerRpc(ServerRpcParams rpcParams = default)
    {
        hasFigurineStartedMoving = true;
    }

    [ClientRpc]
    private void MovedFigureCallbackClientRpc(ClientRpcParams clientRpcParams)
    {
        // If the player can still attack, get the selected figures possible targets
        if (Multiplayer_GameManager.Instance.CanAttack)
        {
            selectedFigurine.GetPossibleTargets();
        }

        OnFigureStopMoving.Invoke(this);
        OnFigureMoved?.Invoke(this);
    }

    public bool DetectSelectFigure(GameObject hit, ServerRpcParams serverRpcParams)
    {
        // Detects if you click on one of your Figurines
        if (hit == null)
        {
            return false;
        }

        if (hit.tag == "Figurine")
        {
            selectedFigurine = hit.GetComponent<Figurine>();

            if (hit.GetComponent<Figurine>().Team == $"Player {playerID}")
            {
                IsHighlightingPositions = false;

                if (OwnerClientId == (ulong)Multiplayer_GameManager.Instance.GameBattleState &&
                    !selectedFigurine.CurrentSpacePos.name.Contains("Infirmary"))
                {
                    // Checks if the player can still move this turn
                    if (Multiplayer_GameManager.Instance.MovementTurns >= 1 && !selectedFigurine.debuffs.ContainsKey(FigurineEffect.StatusEffects.Wait))
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
        if (hit == null)
        {
            return false;
        }
        // Detects if you clicked on a figurine
        if (hit.tag == "Figurine")
        {
            Figurine enemyFigure = hit.GetComponent<Figurine>();

            // Checks if the figure is an enemy
            if (enemyFigure.Team != $"Player {playerID}")
            {
                if (selectedFigurine == null)
                {
                    return false;
                }

                selectedFigurine.GetPossibleTargets();

                // Checks if it is a possible target to your selected figurine
                if (selectedFigurine.PossibleTargets.Contains(enemyFigure.gameObject))
                {
                    // Returns False if the figurine is attacking from a bench
                    if (selectedFigurine.CurrentSpacePos.name.Contains("Bench"))
                    {
                        return false;
                    }

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
        switch (Multiplayer_GameManager.Instance.activeMoveEffect)
        {
            case FigurineEffect.MoveEffects.Pushback:
                List<Tile>[] enemyPossiblePositions = enemyBattleFigure.GetPossiblePositions();
                if (enemyPossiblePositions == null)
                {
                    CompleteMoveEffectClientRpc();
                    OnCompletedMoveEffect?.Invoke(this);
                    return;
                }

                foreach (Tile possiblePosition in enemyPossiblePositions[0])
                {
                    if (hitObject == possiblePosition.gameObject)
                    {
                        Debug.Log("Completed Pushback Move Effect!");
                        StartCoroutine(enemyBattleFigure.MovementSequence(possiblePosition));
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

    private void CancelMoveEffect()
    {
        CompleteMoveEffectClientRpc();
        OnCompletedMoveEffect?.Invoke(this);
    }

    #endregion

    #region External Moves
    public void CompleteExternalMove(GameObject hitObject)
    {
        switch (activeExternalMove)
        {
            case "Fortification":
                if (hitObject.tag != "BoardSpace")
                {
                    return;
                }

                List<Tile>[] possibleSpawnLocations = selectedFigurine.GetPossiblePositions();
                foreach(Tile possibleSpawnLoc in possibleSpawnLocations[0])
                {
                    GameObject rockWallPrefab = Resources.Load<GameObject>("Spawnables/Rock_Spawnable");
                    if (hitObject == possibleSpawnLoc.gameObject)
                    {
                        Debug.Log("Completed Fortification External Move!");
                        GameObject oldRockWall = GameObject.Find(selectedFigurine.Team + " - " + "Rock_Spawnable(Clone)");
                        if (oldRockWall != null)
                        {
                            Destroy(oldRockWall);
                        }

                        // Spawns in the Boulder Wall
                        Vector3 spawnLoc = possibleSpawnLoc.transform.position + new Vector3(0, 5f, 0);
                        
                        GameObject prefabInstance = Instantiate(rockWallPrefab, spawnLoc, selectedFigurine.transform.rotation);
                        Figurine wallFigurine = prefabInstance.GetComponent<Figurine>();
                        string originalName = wallFigurine.name;
                        wallFigurine.CurrentSpacePos = possibleSpawnLoc.gameObject;
                        wallFigurine.Team = selectedFigurine.Team;
                        prefabInstance.GetComponent<NetworkObject>().Spawn();
                        wallFigurine.name = wallFigurine.Team + " - " + wallFigurine.name;
                        wallFigurine.debuffs.Add(FigurineEffect.StatusEffects.Decay, 3);
                        wallFigurine.CheckForSurroundKill();
                        FortificationClientRpc(originalName, wallFigurine.name);
                        break;
                    }
                }
                break;
            case "Fruitful Fury":
                if (hitObject.tag == "Figurine")
                {
                    Figurine clickedFigurine = hitObject.GetComponent<Figurine>();
                    if (clickedFigurine.Team == $"Player {playerID}")
                    {
                        try
                        {
                            if (clickedFigurine.CurrentSpacePos.name.Contains("Infirmary"))
                            {
                                break;
                            }
                        }
                        catch (System.Exception) { }

                        // Fruitfury Fury Effect
                        if (selectedFigurine.buffs.ContainsKey(FigurineEffect.StatusEffects.Lifesteal))
                        {
                            // Safe to use selectedFigurine.buffs[FigurineEffect.StatusEffects.Lifesteal]
                            int lifestealValue = selectedFigurine.buffs[FigurineEffect.StatusEffects.Lifesteal];
                            clickedFigurine.currentHealth += lifestealValue * selectedFigurine.attackStat;
                            if (clickedFigurine.currentHealth > clickedFigurine.totalHealth)
                            {
                                clickedFigurine.currentHealth = clickedFigurine.totalHealth;
                            }
                            clickedFigurine.TakeEffect();
                            selectedFigurine.buffs.Remove(FigurineEffect.StatusEffects.Lifesteal);
                            FruitfulFuryClientRpc(selectedFigurine.name, clickedFigurine.name);
                        }
                    }
                }
                break;
            case "Cleansing Rose":
                if (hitObject.tag == "Figurine")
                {
                    Figurine clickedFigurine = hitObject.GetComponent<Figurine>();
                    if (clickedFigurine.Team == $"Player {playerID}")
                    {
                        try
                        {
                            if (clickedFigurine.CurrentSpacePos.name.Contains("Infirmary"))
                            {
                                break;
                            }
                        }
                        catch (System.Exception) { }

                        // Cleansing Rose Effect
                        if (clickedFigurine.debuffs.Count > 0)
                        {
                            int randomDebuffIndex = UnityEngine.Random.Range(0, clickedFigurine.debuffs.Count);
                            FigurineEffect.StatusEffects randomDebuff = clickedFigurine.debuffs.ElementAt(randomDebuffIndex).Key;
                            clickedFigurine.debuffs.Remove(randomDebuff);
                            RemoveDebuffClientRpc(clickedFigurine.name, randomDebuff);
                        }
                    }
                }
                break;
            case "Rejuvinate":
                if (hitObject.tag == "Figurine")
                {
                    Figurine clickedFigurine = hitObject.GetComponent<Figurine>();
                    if (clickedFigurine.Team == $"Player {playerID}")
                    {
                        try
                        {
                            if (clickedFigurine.CurrentSpacePos.name.Contains("Infirmary"))
                            {
                                break;
                            }
                        }
                        catch (System.Exception) { }

                        // Rejuvinate Effect
                        FigurineEffect.StatusEffects statusEffect = FigurineEffect.StatusEffects.AttackUp;
                        clickedFigurine.buffs[statusEffect] = clickedFigurine.buffs.GetValueOrDefault(statusEffect) + 2;
                        clickedFigurine.TakeEffect();
                        ApplyBuffClientRpc(clickedFigurine.name, statusEffect, 2);
                    }
                }
                break;
            default:
                break;
        }

        
        string externalMoveAnnouncement = null;
        if (selectedFigurine != null)
        {
            externalMoveAnnouncement = $"{selectedFigurine.figurineName} used {activeExternalMove}";
            if (hitObject.tag == "Figurine") { externalMoveAnnouncement += $" on {hitObject.GetComponent<Figurine>().figurineName}"; }
            ;
            externalMoveAnnouncement += "!";
        }
        OnCompletedExternalMove?.Invoke(this);
        CompleteExternalMoveClientRpc(externalMoveAnnouncement);
        activeExternalMove = "";
    }
    #endregion

    #region Update Status Effect Client Rpcs
    [ClientRpc]
    private void ApplyBuffClientRpc(string clickedFigurineName, FigurineEffect.StatusEffects statusEffect, int stacks)
    {
        Figurine clickedFigurine = GameObject.Find(clickedFigurineName).GetComponent<Figurine>();
        clickedFigurine.buffs[statusEffect] = clickedFigurine.buffs.GetValueOrDefault(statusEffect) + stacks;
        clickedFigurine.TakeEffect();
    }

    [ClientRpc]
    private void ApplyDebuffClientRpc(string clickedFigurineName, FigurineEffect.StatusEffects statusEffect, int stacks)
    {
        Figurine clickedFigurine = GameObject.Find(clickedFigurineName).GetComponent<Figurine>();
        clickedFigurine.debuffs[statusEffect] = clickedFigurine.debuffs.GetValueOrDefault(statusEffect) + stacks;
        clickedFigurine.TakeEffect();
    }

    [ClientRpc]
    private void RemoveDebuffClientRpc(string clickedFigurineName, FigurineEffect.StatusEffects statusEffect)
    {
        Figurine clickedFigurine = GameObject.Find(clickedFigurineName).GetComponent<Figurine>();
        clickedFigurine.debuffs.Remove(statusEffect);
        clickedFigurine.TakeEffect();
    }

    [ClientRpc]
    private void RemoveBuffClientRpc(string clickedFigurineName, FigurineEffect.StatusEffects statusEffect)
    {
        Figurine clickedFigurine = GameObject.Find(clickedFigurineName).GetComponent<Figurine>();
        clickedFigurine.buffs.Remove(statusEffect);
        clickedFigurine.TakeEffect();
    }
    #endregion

    #region External Move Client Rpc
    [ClientRpc]
    private void FortificationClientRpc(string originalRockName, string newRockName)
    {

        GameObject rockWallGO = GameObject.Find(originalRockName);
        rockWallGO.name = newRockName;
        Figurine wallFigurine = rockWallGO.GetComponent<Figurine>(); 
        wallFigurine.debuffs.Add(FigurineEffect.StatusEffects.Decay, 3);
    }

    [ClientRpc]
    private void FruitfulFuryClientRpc(string selectedFigureName, string clickedFigurineName)
    {
        Figurine clickedFigurine = GameObject.Find(clickedFigurineName).GetComponent<Figurine>();
        Figurine externalMoveSelectedFigurine = GameObject.Find(selectedFigureName).GetComponent<Figurine>();
        int lifestealValue = externalMoveSelectedFigurine.buffs[FigurineEffect.StatusEffects.Lifesteal];
        clickedFigurine.currentHealth += lifestealValue * selectedFigurine.attackStat;
        if (clickedFigurine.currentHealth > clickedFigurine.totalHealth)
        {
            clickedFigurine.currentHealth = clickedFigurine.totalHealth;
        }
        clickedFigurine.TakeEffect();
        externalMoveSelectedFigurine.buffs.Remove(FigurineEffect.StatusEffects.Lifesteal);
    }
    

    [ClientRpc]
    private void CompleteExternalMoveClientRpc(string externalMoveAnnouncement)
    {
        activeExternalMove = "";
        OnCompletedExternalMove?.Invoke(this);
        ExternalMoveAnnouncement?.Invoke(externalMoveAnnouncement);
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

        Debug.Log($"BattleStartClientRpc called on client {NetworkManager.Singleton.LocalClientId}, OwnerClientId: {OwnerClientId}");
        OnBattleStart?.Invoke(this);

    }

    #region Manage Figurine Memory
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

    private void ClearSelectedFigurine(MultiplayerBattleState previousState, MultiplayerBattleState newState)
    {
        selectedFigurine = null;
    }
    #endregion

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

    #region Player Teams
    private void LoadPlayerTeams()
    {
        Debug.Log("Loading Player Teams after event was invoked!");
        string savedTeamString = File.ReadAllText(Application.persistentDataPath + "/savedTeam.txt");

        if (IsOwner)
        {
            SendPlayerTeamToServerRpc(savedTeamString);
        }
    }

    [ServerRpc]
    public void SendPlayerTeamToServerRpc(string savedTeamString, ServerRpcParams serverRpcParams = default)
    {
        Debug.Log("Loading Player Team Data!");
        PlayerTeamData playerTeamData = JsonUtility.FromJson<PlayerTeamData>(savedTeamString);
        for (int i = 0; i < playerTeamData.figureNames.Length; i++)
        {
            playerTeamPrefabs[i] = Resources.Load<GameObject>($"Figurines/{playerTeamData.figureNames[i]}");
        }
        OnPartyLoaded?.Invoke(this);

        UpdatePlayerTeamClientRpc(savedTeamString);
    }

    [ClientRpc]
    private void UpdatePlayerTeamClientRpc(string savedTeamString)
    {
        PlayerTeamData playerTeamData = JsonUtility.FromJson<PlayerTeamData>(savedTeamString);
        for (int i = 0;i < playerTeamData.figureNames.Length;i++)
        {
            playerTeamPrefabs[i] = Resources.Load<GameObject>($"Figurines/{playerTeamData.figureNames[i]}");
        }
    }
    #endregion

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
    public void GamePreparation(MultiplayerBattleState previousState, MultiplayerBattleState newState)
    {
        GamePreparation();
    }

    public void GamePreparation()
    {
        Debug.Log("Preparing Game!");
        Multiplayer_GameManager.OnChangeTurn -= GamePreparation;

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
        if (File.Exists(Application.persistentDataPath + "/savedTeam.txt"))
        {
            string saveString = File.ReadAllText(Application.persistentDataPath + "/savedTeam.txt");

            PlayerTeamData playerTeamData = JsonUtility.FromJson<PlayerTeamData>(saveString);
            for (int i = 0; i < playerTeamData.figureNames.Length; i++)
            {
                Debug.Log("Figurine Name : " + playerTeamData.figureNames[i]);
                playerTeamPrefabs[i] = Resources.Load<GameObject>("Figurines/" + playerTeamData.figureNames[i]);
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
        public string[] figureNames;
    }
}
