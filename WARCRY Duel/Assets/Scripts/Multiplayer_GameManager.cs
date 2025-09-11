using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System;
using System.Linq;

#if DEDICATED_SERVER
using Unity.Services.Multiplay;
#endif

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

    [SerializeField] private int playerDamage = 0;
    [SerializeField] private int enemyDamage = 0;

    public FigurineEffect.MoveEffects activeMoveEffect;
    public bool waitingForCompletedMoveEffect = false;

    // Player References
    [SerializeField] public Multiplayer_Player player1;
    [SerializeField] public Multiplayer_Player player2;
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
        Debug.Log("GameManager Start!");
#if DEDICATED_SERVER
        await MultiplayService.Instance.UnreadyServerAsync();
#endif
        if (IsServer)
        {
            Debug.Log("Subcribing Events in GameManager as Server!");

            NetworkManager.SceneManager.OnLoadComplete += StartGame;
            PlayerUI.OnEndTurn += ChangeTurn;
            Multiplayer_Player.OnBattleStart += StartCombat;
            BattleUI.OnBattleMoveChosen += CombatClash;
            MoveManager.OnMoveEnd += EndCombat;
            Multiplayer_Player.OnCompletedMoveEffect += CompletedMoveEffect;
            Multiplayer_Player.OnCompletedExternalMove += CompletedExternalMove;
            Figurine.OnStopMoving += CheckForWin;

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

    void StartGame(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        Debug.Log("Starting Game! Setting state to Player One's Turn!");
        GameBattleState = MultiplayerBattleState.PLAYERONETURN;
        UpdateGameStateClientRpc(MultiplayerBattleState.START, MultiplayerBattleState.PLAYERONETURN);

        Debug.Log("Invoking Game Start Event!");
        OnGameStart?.Invoke();
    }

    [ClientRpc]
    private void UpdateGameStateClientRpc(MultiplayerBattleState previousState, MultiplayerBattleState newState)
    {
        Debug.Log("Changing Game State!");
        GameBattleState = newState;
        Debug.Log("Game Battle State : " + GameBattleState);
        OnChangeTurn?.Invoke(previousState, newState);
    }

    public void ChangeTurn()
    {
        if (IsServer)
        {
            Debug.Log("Changing Turn!");
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

            UpdateMoveCooldowns(GameBattleState);

            // Updates Clients Gamestate
            UpdateGameStateClientRpc(previousState, GameBattleState);


        }

    }

    private void CheckForWin(Figurine figure)
    {
        if (IsServer)
        {
            Debug.Log("Checking for Win!");

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
            Debug.Log("Updating Move Cooldowns on the Server!");

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
        Debug.Log("Updating Move Cooldown on Client Side");

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
        Debug.Log("Updating Combat State");
        FigureCombatState = newState;
        OnChangeCombatTurn?.Invoke(previousState, newState);
        activeMoveEffect = FigurineEffect.MoveEffects.None;
        MoveEffectState = MoveEffectStateEnum.INACTIVE;
    }

    private void CombatClash()
    {
        // Checks if both players have moves selected
        Debug.Log("Player 1 Combat Move : " + player1.combatMove);
        Debug.Log("Player 2 Combat Move : " + player2.combatMove);

        if (player1.combatMove != null &&
            player2.combatMove != null)
        {
            // Start Clash
            Debug.Log("Starting Clash!");
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
        Debug.Log("Ending Combat");
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

        Debug.Log("ATTACKER MOVE EFFECT COUNT : " + attacker.moveEffects.Count);
        Debug.Log("DEFENDER MOVE EFFECT COUNT : " + defender.moveEffects.Count);
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
        Debug.Log("Ending Combat Client Rpc");
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
                    case FigurineEffect.MoveEffects.Pushback:
                        activeMoveEffect = FigurineEffect.MoveEffects.Pushback;
                        if (moveEffectPlayer == player1)
                        {
                            MoveEffectState = MoveEffectStateEnum.PLAYERTWO;
                        }
                        else if (moveEffectPlayer == player2)
                        {
                            MoveEffectState = MoveEffectStateEnum.PLAYERONE;
                        }

                        InitiateMoveEffectClientRpc(moveEffect.Key.ToString(), MoveEffectState.ToString());
                        waitingForCompletedMoveEffect = true;
                        break;
                    default:
                        break;
                }

                yield return new WaitUntil(() => waitingForCompletedMoveEffect == false);

                activeMoveEffect = FigurineEffect.MoveEffects.None;
            }

            moveEffectPlayer = defender;
        }

        MoveEffectState = MoveEffectStateEnum.INACTIVE;
        activeMoveEffect = FigurineEffect.MoveEffects.None;
        ChangeTurn();
        
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
