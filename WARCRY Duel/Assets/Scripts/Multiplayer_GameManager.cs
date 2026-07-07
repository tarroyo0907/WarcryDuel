using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Unity.Netcode;

using UnityEditor.Experimental.GraphView;

using UnityEngine;
using UnityEngine.SceneManagement;

using static FigurineEffect;


public class Multiplayer_GameManager : NetworkBehaviour
{
    #region Delegates
    public delegate void GameManagerHandler();
    public delegate void ChangeTurnHandler(MultiplayerBattleState previousState, MultiplayerBattleState newState);
    public delegate void CombatTurnHandler(FigureCombatEnum previousState, FigureCombatEnum newState);
    public delegate void MoveEffectHandler(FigurineEffect.MoveEffects moveEffect);
    #endregion

    #region Events
    public static event GameManagerHandler OnGameStart;
    public static event ChangeTurnHandler OnChangeTurn;
    public static event GameManagerHandler EndGameEvent;

    public static event GameManagerHandler OnCombatStart;
    public static event CombatTurnHandler OnChangeCombatTurn;
    public static event GameManagerHandler InitiateMoves;
    public static event GameManagerHandler EndCombatEvent;
    public static event GameManagerHandler OnCancelledMoveEffect;
    public static event GameManagerHandler LoadPlayerTeams;

    public static event MoveEffectHandler OnInitiateMoveEffect;
    #endregion

    #region Fields
    public enum MultiplayerBattleState { START, PLAYERONETURN, PLAYERTWOTURN, GAMEOVER };
    public MultiplayerBattleState GameBattleState = MultiplayerBattleState.START;
    public string gameWinner = null;

    public enum FigureCombatEnum { START, CLASH, END }
    public FigureCombatEnum FigureCombatState = FigureCombatEnum.START;

    public enum MoveEffectStateEnum { INACTIVE, PLAYERONE, PLAYERTWO}
    public MoveEffectStateEnum MoveEffectState = MoveEffectStateEnum.INACTIVE;

    public static Multiplayer_GameManager Instance = null;

    [Header("\nGameplay Fields")]
    [SerializeField] private int movementTurns = 1;
    [SerializeField] private bool canAttack = true;
    [SerializeField] private int combatMovesCompleted = 0;
    [SerializeField] private GameObject BattleObjectLoc1;
    [SerializeField] private GameObject BattleObjectLoc2;
    public int currentTurn = 0;

    [SerializeField] private int playerDamage = 0;
    [SerializeField] private int enemyDamage = 0;

    public FigurineEffect.MoveEffects activeMoveEffect;
    public bool waitingForCompletedMoveEffect = false;

    // Player References
    [SerializeField] public Multiplayer_Player player1;
    [SerializeField] public Multiplayer_Player player2;

    private int playerTeamsLoaded = 0;
    #endregion

    #region Properties
    public int MovementTurns { get { return movementTurns; } set { movementTurns = value; } }
    public bool CanAttack { get { return canAttack; } set { canAttack = value; } }
    #endregion

    #region Base Methods
    // Methods
    private void Awake()
    {
        Instance = this;

        NetworkManager.SceneManager.OnLoadComplete += SceneLoaded;

    }

    // Start is called before the first frame update
    async void Start()
    {
        if (IsServer)
        {
            Debug.Log("Subcribing Events in GameManager as Server!");
            PlayerUI.OnEndTurn += ChangeTurn;
            Multiplayer_Player.OnBattleStart += StartCombat;
            BattleUI.OnBattleMoveChosen += CombatClash;
            MoveManager.OnMoveEnd += EndCombat;
            Multiplayer_Player.OnCompletedMoveEffect += CompletedMoveEffect;
            Multiplayer_Player.OnCompletedExternalMove += CompletedExternalMove;
            Figurine.OnStopMoving += CheckForWin;
            NetworkManager.SceneManager.OnLoadEventCompleted += StartGame;
            Multiplayer_Player.OnPartyLoaded += OnPlayerPartyLoaded;

            // Grabs both players if they are connected
            Debug.Log("Connected Clients Count: " + NetworkManager.Singleton.ConnectedClients.Count);
            for (int i=0; i < NetworkManager.Singleton.ConnectedClients.Count; i++)
            {
                Debug.Log("Client ID: " + NetworkManager.Singleton.ConnectedClientsIds[i]);
            }
            if (NetworkManager.Singleton.ConnectedClients.Count >= 2)
            {
                player1 = NetworkManager.Singleton.ConnectedClients[1].PlayerObject.GetComponent<Multiplayer_Player>();
                player2 = NetworkManager.Singleton.ConnectedClients[2].PlayerObject.GetComponent<Multiplayer_Player>();
            }

        }
    }

    // Update is called once per frame
    void Update()
    {

    }
    #endregion

    #region GameStates

    void SceneLoaded(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        Debug.Log("Scene Successfully Loaded!");
    }

    void StartGame(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log("Running Start Game Coroutine!");
        StartCoroutine(StartGameCoroutine());
    }

    IEnumerator StartGameCoroutine()
    {
        Debug.Log("Invoking Player Team Loading!");
        LoadPlayerTeamClientRpc();

        yield return new WaitUntil(() => playerTeamsLoaded == 2);

        Debug.Log("Starting Game! Setting state to Player One's Turn!");
        GameBattleState = MultiplayerBattleState.PLAYERONETURN;
        UpdateGameStateClientRpc(MultiplayerBattleState.START, MultiplayerBattleState.PLAYERONETURN);
        currentTurn++;

        Debug.Log("Invoking Game Start Event!");
        OnGameStart?.Invoke();
    }

    [ClientRpc]
    private void LoadPlayerTeamClientRpc()
    {
        LoadPlayerTeams?.Invoke();
    }


    [ClientRpc]
    private void UpdateGameStateClientRpc(MultiplayerBattleState previousState, MultiplayerBattleState newState)
    {
        Debug.Log("Changing Game State!");
        GameBattleState = newState;
        Debug.Log("Game Battle State : " + GameBattleState);
        currentTurn++;
        OnChangeTurn?.Invoke(previousState, newState);
    }

    private void OnPlayerPartyLoaded(Multiplayer_Player player)
    {
        Debug.Log("A party has been loaded! Counter increased! " + playerTeamsLoaded);
        playerTeamsLoaded++;
    }

    public void ChangeTurn()
    {
        if (IsServer)
        {
            MultiplayerBattleState previousState = GameBattleState;
            switch (GameBattleState)
            {
                case (MultiplayerBattleState.PLAYERONETURN):
                    GameBattleState = MultiplayerBattleState.PLAYERTWOTURN;
                    break;

                case (MultiplayerBattleState.PLAYERTWOTURN):
                    GameBattleState = MultiplayerBattleState.PLAYERONETURN;
                    break;

                default:
                    break;
            }
            movementTurns = 1;
            canAttack = true;
            activeMoveEffect = FigurineEffect.MoveEffects.None;
            MoveEffectState = MoveEffectStateEnum.INACTIVE;
            player1.selectedFigurine = null;
            player2.selectedFigurine = null;
            currentTurn++;

            UpdateMoveCooldowns(GameBattleState);

            // Updates Clients Gamestate
            UpdateGameStateClientRpc(previousState, GameBattleState);


        }

    }

    private void CheckForWin(Figurine figure)
    {
        if (IsServer)
        {
            // If player 2's figurine lands on Player 1's Goal Space.
            if (figure.CurrentSpacePos.name == "PlayerOneGoalSpace" &&
                figure.Team == "Player 2")
            {
                Debug.Log("Player 2 is the Winner!");
                gameWinner = "Player 2";
            }
            // If Player 1's figurine lands on Player 2's Goal Space.
            else if (figure.CurrentSpacePos.name == "PlayerTwoGoalSpace" &&
                figure.Team == "Player 1")
            {
                Debug.Log("Player 1 is the Winner!");
                gameWinner = "Player 1";
            }
            // If there is no winner yet,
            else
            {
                // Return from function.
                Debug.Log("No Winner Yet");
                return;
            }

            // End the game
            Debug.Log("Ending the Game!");
            MultiplayerBattleState previousState = GameBattleState;
            GameBattleState = MultiplayerBattleState.GAMEOVER;
            UpdateGameStateClientRpc(previousState, GameBattleState);

            EndGameClientRpc(gameWinner);
        }
    }

    [ClientRpc]
    private void EndGameClientRpc(string Winner)
    {
        Debug.Log("Ending Game on Client");

        // Updates the Game Winner
        gameWinner = Winner;

        // Invokes the End Game Event
        EndGameEvent?.Invoke();
    }

    private void UpdateMoveCooldowns(MultiplayerBattleState newState)
    {
        // Ensures that the function is only running on the server
        if (IsServer)
        {
            // Compiles a list of the figurines who are owned by the player whose turn it is
            List<Figurine> figurineList = new List<Figurine>();
            List<NetworkObjectReference> objectReferenceList = new List<NetworkObjectReference>();

            // Adds the figurines depending on the turn
            if (newState == MultiplayerBattleState.PLAYERONETURN) { objectReferenceList.AddRange(player1.PlayerTeamUnits); }
            if (newState == MultiplayerBattleState.PLAYERTWOTURN) { objectReferenceList.AddRange(player2.PlayerTeamUnits); }

            foreach (NetworkObjectReference objectReference in objectReferenceList)
            {
                objectReference.TryGet(out NetworkObject figurineNetworkObject);
                Figurine currentFigurine = figurineNetworkObject.gameObject.GetComponent<Figurine>();
                figurineList.Add(currentFigurine);
            }

            // Decrease the move cooldown for each move of every figurine
            foreach (Figurine figurine in figurineList)
            {
                // Creates a temp list of the figurines moves
                List<FigurineMove> figurineMovesList = new List<FigurineMove>();
                figurineMovesList.Add(figurine.move1);
                figurineMovesList.Add(figurine.move2);
                figurineMovesList.Add(figurine.move3);

                // Iterates through each move
                foreach (FigurineMove move in figurineMovesList)
                {
                    // Reduces the move cooldown by 1
                    figurine.moveCooldowns[move]--;

                    // Prevents the move cooldown from going below 0
                    if (figurine.moveCooldowns[move] <= 0)
                    {
                        figurine.moveCooldowns[move] = 0;
                    }

                    // Sends a ClientRPC to update the value on the client's side
                    UpdateMoveCooldownsClientRpc(figurine.gameObject, move.moveName, figurine.moveCooldowns[move]);
                }
            }
        }
    }

    [ClientRpc]
    private void UpdateMoveCooldownsClientRpc(NetworkObjectReference networkObject, string moveName, int moveCooldownValue)
    {
        // Grabs the Figurine Component from the Network Object Reference
        networkObject.TryGet(out NetworkObject figurineNetworkObject);
        Figurine figurine = figurineNetworkObject.gameObject.GetComponent<Figurine>();

        // Grab the Move using the specified name
        FigurineMove figurineMove = Resources.Load<FigurineMove>($"Moves/{moveName}");

        // Update the move cooldown
        figurine.moveCooldowns[figurineMove] = moveCooldownValue;
    }

    #endregion

    #region CombatSystem
    private void StartCombat(Multiplayer_Player player)
    {
        // Sets the combat state to the state that matches the player's ID
        FigureCombatState = (FigureCombatEnum)player.OwnerClientId;

        // Moves Figures to their Battle Locations depending on their client ID
        if (player.OwnerClientId == 1)
        {
            player.playerBattleFigure.transform.position = BattleObjectLoc1.transform.position;
            player.playerBattleFigure.transform.rotation = BattleObjectLoc1.transform.rotation;

            player.enemyBattleFigure.transform.position = BattleObjectLoc2.transform.position;
            player.enemyBattleFigure.transform.rotation = BattleObjectLoc2.transform.rotation;
        }
        else if (player.OwnerClientId == 2)
        {
            player.playerBattleFigure.transform.position = BattleObjectLoc2.transform.position;
            player.playerBattleFigure.transform.rotation = BattleObjectLoc2.transform.rotation;

            player.enemyBattleFigure.transform.position = BattleObjectLoc1.transform.position;
            player.enemyBattleFigure.transform.rotation = BattleObjectLoc1.transform.rotation;
        }

        OnCombatStart?.Invoke();
        UpdateCombatStateClientRpc(FigureCombatEnum.START, FigureCombatState);
    }


    [ClientRpc]
    private void UpdateCombatStateClientRpc(FigureCombatEnum previousState, FigureCombatEnum newState)
    {
        FigureCombatState = newState;
        OnChangeCombatTurn?.Invoke(previousState, newState);
        activeMoveEffect = FigurineEffect.MoveEffects.None;
        MoveEffectState = MoveEffectStateEnum.INACTIVE;
    }

    private void CombatClash()
    {
        // Checks if both players have moves selected
        if (player1.combatMove != null &&
            player2.combatMove != null)
        {
            // Start Clash
            InitiateMoves?.Invoke();
            CombatClashClientRpc();
        }
    }

    [ClientRpc]
    private void CombatClashClientRpc()
    {
        Debug.Log("Initiating Moves");
        InitiateMoves?.Invoke();
    }

    private void EndCombat()
    {
        Debug.Log("Checking for End Combat");
        // Each time a move is completed, increments this value
        combatMovesCompleted++;

        // Once both moves are completed, ends combat
        if (combatMovesCompleted == 2)
        {
            // Starts the End Combat Coroutine
            combatMovesCompleted = 0;
            StartCoroutine(EndCombatCoroutine());
        }
    }

    private IEnumerator EndCombatCoroutine()
    {
        // Waits a bit before ending the combat
        yield return new WaitForSeconds(2.5f);

        // Moves Combat Figurines back to their original positions
        player1.playerBattleFigure.transform.position = player1.playerBattleFigure.CurrentSpacePos.transform.position;
        player1.playerBattleFigure.ReturnToOriginalAlignment();

        player1.enemyBattleFigure.transform.position = player1.enemyBattleFigure.CurrentSpacePos.transform.position;
        player1.enemyBattleFigure.ReturnToOriginalAlignment();

        // Invokes the End Combat Event
        EndCombatEvent?.Invoke();

        Multiplayer_Player attacker = null;
        Multiplayer_Player defender = null;
        if (GameBattleState == MultiplayerBattleState.PLAYERONETURN) { attacker = player1; defender = player2; }
        else if (GameBattleState == MultiplayerBattleState.PLAYERTWOTURN) { attacker = player2; defender = player1; }

        if (attacker.moveEffects.Count > 0 || defender.moveEffects.Count > 0)
        {
            StartCoroutine(InitiatingMoveEffects(attacker, defender));
        }
        else
        {
            ChangeTurn();
        }

        EndCombatClientRpc();
    }

    [ClientRpc]
    private void EndCombatClientRpc()
    {
        // Invokes the End Combat Event
        EndCombatEvent?.Invoke();
    }

    #region Move Effects
    IEnumerator InitiatingMoveEffects(Multiplayer_Player attacker, Multiplayer_Player defender)
    {
        yield return new WaitForSeconds(1f);

        Multiplayer_Player moveEffectPlayer = attacker;
        for (int i = 0; i < 2; i++)
        {
            for (int k = 0; k < moveEffectPlayer.moveEffects.Count; k++)
            {
                KeyValuePair<FigurineEffect.MoveEffects, int> moveEffect = moveEffectPlayer.moveEffects.ElementAt(k);

                switch (moveEffect.Key)
                {
                    case MoveEffects.Pushback:
                        PushbackMoveEffect(moveEffectPlayer, moveEffect);
                        yield return new WaitUntil(() => waitingForCompletedMoveEffect == false);
                        break;
                    case MoveEffects.GrowingFlames:
                        activeMoveEffect = FigurineEffect.MoveEffects.GrowingFlames;
                        waitingForCompletedMoveEffect = true;
                        StartCoroutine(GrowingFlamesMoveEffect(moveEffectPlayer, attacker, defender));
                        yield return new WaitUntil(() => waitingForCompletedMoveEffect == false);
                        break;
                    default:
                        break;
                }

                

                activeMoveEffect = FigurineEffect.MoveEffects.None;
            }

            moveEffectPlayer = defender;
        }

        MoveEffectState = MoveEffectStateEnum.INACTIVE;
        activeMoveEffect = FigurineEffect.MoveEffects.None;
        ChangeTurn();
        
    }

    private void PushbackMoveEffect(Multiplayer_Player moveEffectPlayer, KeyValuePair<FigurineEffect.MoveEffects, int> moveEffect)
    {
        activeMoveEffect = FigurineEffect.MoveEffects.Pushback;
        if (moveEffectPlayer == player1)
        {

            MoveEffectState = MoveEffectStateEnum.PLAYERTWO;
        }
        else if (moveEffectPlayer == player2)
        {
            MoveEffectState = MoveEffectStateEnum.PLAYERONE;
        }

        // Check Move Effect
        List<Tile>[] enemyPossiblePositions = moveEffectPlayer.playerBattleFigure.GetPossiblePositions();
        if (enemyPossiblePositions == null)
        {
            Multiplayer_GameManager.Instance.activeMoveEffect = FigurineEffect.MoveEffects.None;
            CancelMoveEffectClientRpc();
            waitingForCompletedMoveEffect = false;
            return;
        }
        InitiateMoveEffectClientRpc(moveEffect.Key.ToString(), MoveEffectState.ToString());
        waitingForCompletedMoveEffect = true;
    }

    private IEnumerator GrowingFlamesMoveEffect(Multiplayer_Player moveEffectPlayer, Multiplayer_Player attacker, Multiplayer_Player defender)
    {
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
        Multiplayer_Player moveEffectUser;
        if (moveEffectPlayer == attacker)
        {
            moveEffectUser = defender;
        }
        else
        {
            moveEffectUser = attacker;

        }

        // Checks each accessible tile connecting to the parent tile
        foreach (Tile possibleTargetTile in currentTile.AccessibleTiles)
        {
            foreach (GameObject figure in Figurines)
            {
                if (figure.name != moveEffectUser.playerBattleFigure.gameObject.name)
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

            string figurineNames = string.Join(",", adjacentFigurines.ConvertAll(f => f.name).ToArray());
            Debug.Log($"Sending Growing Flames Effect to clients for figurines: {figurineNames}");
            GrowingFlamesEffectClientRpc(figurineNames, attacker.playerBattleFigure.name);
        }

        moveEffectPlayer.NotifyMoveEffectCompleted();
        waitingForCompletedMoveEffect = false;
    }

    [ClientRpc]
    void GrowingFlamesEffectClientRpc(string figurines, string attackerName)
    {
        Debug.Log("Growing Flames Effect Client RPC called for figurines: " + figurines);
        string[] figurineNames = figurines.Split(',');
        GameObject attackerGO = GameObject.Find(attackerName);
        Figurine attacker = attackerGO.GetComponent<Figurine>();

        foreach (string figurineName in figurineNames)
        {
            GameObject figurineGO = GameObject.Find(figurineName);
            Figurine figurine = figurineGO.GetComponent<Figurine>();
            figurine.incomingEffect.IncomingDamage += (int)(attacker.attackStat * 2.0f);
            figurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Burn, 2);
            figurine.TakeEffect();
        }
    }

    [ClientRpc]
    void InitiateMoveEffectClientRpc(string moveEffect, string newMoveEffectState)
    {
        // Updates the Game Battle State
        Enum.TryParse(newMoveEffectState, out MoveEffectStateEnum newMoveEffectStateEnum);
        MoveEffectState = newMoveEffectStateEnum;

        // Grabs the Move Effect Enum
        Enum.TryParse(moveEffect, out FigurineEffect.MoveEffects MoveEffectEnum);
        activeMoveEffect = MoveEffectEnum;

        // Announces that the move effect is currently being initiated
        OnInitiateMoveEffect?.Invoke(MoveEffectEnum);

    }

    [ClientRpc]
    void CancelMoveEffectClientRpc()
    {
        OnCancelledMoveEffect.Invoke();
    }

    void CompletedMoveEffect(Multiplayer_Player player)
    {
        waitingForCompletedMoveEffect = false;
    }

    #endregion

    #region External Moves
    private void CompletedExternalMove(Multiplayer_Player player)
    {
        ChangeTurn();
    }
    #endregion
    #endregion
}
