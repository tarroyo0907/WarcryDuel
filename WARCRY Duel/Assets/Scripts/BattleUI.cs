using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;

using Unity.Netcode;
using Unity.Services.Matchmaker.Models;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BattleUI : NetworkBehaviour
{
    #region Delegates
    // Delegates
    public delegate void BattleMoveHandler(FigurineMove move, Figurine playerFigurine, Figurine enemyFigurine);
    public delegate void BattleMoveChosenHandler();
    #endregion

    #region Events
    // Events
    public static event BattleMoveHandler OnUseBattleMove;
    public static event BattleMoveChosenHandler OnBattleMoveChosen;
    #endregion

    #region Gameplay Fields
    // Fields

    [Header("\n Move 1 Button")]
    [SerializeField] private Button move1Button;
    [SerializeField] private GameObject move1Overlay;
    [SerializeField] private TMPro.TextMeshProUGUI move1OverlayText;
    [SerializeField] private TextMeshProUGUI move1PassiveText;

    [Header("\n Move 2 Button")]
    [SerializeField] private Button move2Button;
    [SerializeField] private GameObject move2Overlay;
    [SerializeField] private TMPro.TextMeshProUGUI move2OverlayText;
    [SerializeField] private TextMeshProUGUI move2PassiveText;

    [Header("\n Move 3 Button")]
    [SerializeField] private Button move3Button;
    [SerializeField] private GameObject move3Overlay;
    [SerializeField] private TMPro.TextMeshProUGUI move3OverlayText;
    [SerializeField] private TextMeshProUGUI move3PassiveText;

    [Header("\n UI References")]
    [SerializeField] private Canvas battleCanvas;
    [SerializeField] private GameObject timerPanel;
    [SerializeField] private GameObject turnCounterPanel;
    [SerializeField] private TMPro.TextMeshProUGUI moveTimerText;
    [SerializeField] private float MoveDisplayLabelLength;
    [SerializeField] private GameObject statusConditionPrefab;

    [Header("\n Player Figure UI References")]
    [SerializeField] private GameObject playerFigureHP;
    [SerializeField] private Slider playerFigureHPSlider;
    [SerializeField] private TMPro.TextMeshProUGUI playerFigureHPText;
    [SerializeField] private TMPro.TextMeshProUGUI playerFigureName;
    [SerializeField] private GameObject playerStatusConditions;

    [SerializeField] private GameObject playerMoveLabel;
    [SerializeField] private TMPro.TextMeshProUGUI playerMoveDisplayText;


    [Header("\n Enemy Figure UI References")]
    [SerializeField] private GameObject enemyFigureHP;
    [SerializeField] private Slider enemyFigureHPSlider;
    [SerializeField] private TMPro.TextMeshProUGUI enemyFigureHPText;
    [SerializeField] private TMPro.TextMeshProUGUI enemyFigureName;
    [SerializeField] private GameObject enemyStatusConditions;

    [SerializeField] private GameObject enemyMoveLabel;
    [SerializeField] private TMPro.TextMeshProUGUI enemyMoveDisplayText;

    private Multiplayer_Player localPlayer;
    private int moveTimer;
    #endregion

    private void Awake()
    {


    }
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("BATTLE UI IS SERVER : " + IsServer);
        if (!IsServer)
        {
            Multiplayer_Player.OnBattleStart += ToggleBattleUICanvas;
            Multiplayer_Player.OnBattleStart += DisplayBattleUI;
            MoveManager.OnMoveEnd += UpdateFigureHP;
            MoveManager.OnMoveUse += DisplayEnemyMoveLabel;
            Multiplayer_GameManager.EndCombatEvent += EndBattleUI;
            Multiplayer_GameManager.EndCombatEvent += ToggleBattleUICanvas;

        }

        if (IsServer)
        {
            Multiplayer_Player.OnBattleStart += StartMoveCountdown;
            Multiplayer_GameManager.InitiateMoves += ClearMoveCountdown;
            return;
        }

        move1Button.onClick.AddListener(() => MoveSelected(move1Button));
        move2Button.onClick.AddListener(() => MoveSelected(move2Button));
        move3Button.onClick.AddListener(() => MoveSelected(move3Button));

        battleCanvas = GetComponent<Canvas>();

        // Switches UI to project to the player's camera perspective
        battleCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        if (NetworkManager.Singleton.LocalClientId == 1)
        {
            battleCanvas.worldCamera = GameObject.Find("Player 1 Battle Camera").GetComponent<Camera>();
        }

        if (NetworkManager.Singleton.LocalClientId == 2)
        {
            battleCanvas.worldCamera = GameObject.Find("Player 2 Battle Camera").GetComponent<Camera>();
        }

        playerFigureHP.SetActive(false);
        enemyFigureHP.SetActive(false);
        timerPanel.SetActive(false);
        turnCounterPanel.SetActive(false);

        localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Multiplayer_Player>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void DisplayBattleUI(Multiplayer_Player player)
    {
        Debug.Log("DISPLAYING BATTLE UI");

        // Enables persistent UI
        playerFigureHP.SetActive(true);
        enemyFigureHP.SetActive(true);
        timerPanel.SetActive(true);
        turnCounterPanel.SetActive(true);

        // Updates figure's health
        #region
        Figurine playerBattleFigure = localPlayer.playerBattleFigure;
        Figurine enemyBattleFigure = localPlayer.enemyBattleFigure;

        playerFigureHPSlider.maxValue = playerBattleFigure.totalHealth;
        playerFigureHPSlider.value = playerBattleFigure.currentHealth;
        playerFigureHPText.text = $"HP : {playerBattleFigure.currentHealth}/{playerBattleFigure.totalHealth}";
        playerFigureName.text = playerBattleFigure.figurineName;

        // Displays Player Figure's Status Conditions
        foreach (Transform transform in playerStatusConditions.transform)
        {
            Destroy(transform.gameObject);
        }

        float spawnPosIncrement = 0f;
        float incrementAmount = 80f;
        foreach (FigurineEffect.StatusEffects statusCondition in playerBattleFigure.buffs.Keys)
        {
            DisplayStatusCondition(playerStatusConditions, playerBattleFigure.buffs, spawnPosIncrement, statusCondition, "Buff");
            spawnPosIncrement -= incrementAmount;
        }

        foreach (FigurineEffect.StatusEffects statusCondition in playerBattleFigure.debuffs.Keys)
        {
            DisplayStatusCondition(playerStatusConditions, playerBattleFigure.debuffs, spawnPosIncrement, statusCondition, "Debuff");
            spawnPosIncrement -= incrementAmount;
        }

        enemyFigureHPSlider.maxValue = enemyBattleFigure.totalHealth;
        enemyFigureHPSlider.value = enemyBattleFigure.currentHealth;
        enemyFigureHPText.text = $"HP : {enemyBattleFigure.currentHealth}/{enemyBattleFigure.totalHealth}";
        enemyFigureName.text = enemyBattleFigure.figurineName;

        // Displays Enemy Figure's Status Conditions
        foreach (Transform transform in enemyStatusConditions.transform)
        {
            Destroy(transform.gameObject);
        }

        spawnPosIncrement = 0f;
        foreach (FigurineEffect.StatusEffects statusCondition in enemyBattleFigure.buffs.Keys)
        {
            DisplayStatusCondition(enemyStatusConditions, enemyBattleFigure.buffs, spawnPosIncrement, statusCondition, "Buff");
            spawnPosIncrement -= incrementAmount;
        }

        foreach (FigurineEffect.StatusEffects statusCondition in enemyBattleFigure.debuffs.Keys)
        {
            DisplayStatusCondition(enemyStatusConditions, enemyBattleFigure.debuffs, spawnPosIncrement, statusCondition, "Debuff");
            spawnPosIncrement -= incrementAmount;
        }

        #endregion

        #region Displays Move Options
        Button[] buttons = new Button[] { move1Button, move2Button, move3Button };
        FigurineMove[] moveArray = new FigurineMove[] { playerBattleFigure.move1, playerBattleFigure.move2, playerBattleFigure.move3 };
        GameObject[] moveOverlayArray = new GameObject[] { move1Overlay, move2Overlay, move3Overlay };
        TextMeshProUGUI[] moveOverlayTextArray = new TextMeshProUGUI[] { move1OverlayText, move2OverlayText, move3OverlayText };
        TextMeshProUGUI[] passiveTextArray = new TextMeshProUGUI[] { move1PassiveText, move2PassiveText, move3PassiveText };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (moveArray[i].moveType == FigurineMove.moveTypes.Null)
            {
                //continue;
            }

            buttons[i].image.sprite = moveArray[i].moveIcon;
            if (moveArray[i].moveType != FigurineMove.moveTypes.Action)
            {
                passiveTextArray[i].gameObject.SetActive(true);
                passiveTextArray[i].text = moveArray[i].moveType.ToString();
            }
            else
            {
                passiveTextArray[i].gameObject.SetActive(false);
            }

            if (playerBattleFigure.moveCooldowns[moveArray[i]] > 0)
            {
                moveOverlayArray[i].SetActive(true);
                moveOverlayTextArray[i].text = playerBattleFigure.moveCooldowns[moveArray[i]].ToString();
            }
            else
            {
                moveOverlayArray[i].SetActive(false);
            }
            buttons[i].transform.parent.gameObject.SetActive(true);
        }
        #endregion
    }

    void DisplayStatusCondition(GameObject statusConditionPanel, Dictionary<FigurineEffect.StatusEffects, int> statusEffects, float spawnPosIncrement, FigurineEffect.StatusEffects statusCondition, string effectType)
    {
        GameObject spawnedStatusEffect = Instantiate(statusConditionPrefab, statusConditionPanel.transform.position, statusConditionPanel.transform.rotation, statusConditionPanel.transform);
        if (statusConditionPanel == enemyStatusConditions)
        {
            spawnedStatusEffect.GetComponent<RectTransform>().anchoredPosition += new Vector2(-spawnPosIncrement, 0);
        }
        else if (statusConditionPanel == playerStatusConditions)
        {
            spawnedStatusEffect.GetComponent<RectTransform>().anchoredPosition += new Vector2(spawnPosIncrement, 0);
        }


        if (effectType == "Debuff")
        {
            spawnedStatusEffect.GetComponent<Image>().color = Color.red;
        }
        else if(effectType == "Buff")
        {
            spawnedStatusEffect.GetComponent<Image>().color = Color.green;
        }

        Image statusConditionIcon = spawnedStatusEffect.transform.GetChild(0).GetComponent<Image>();
        statusConditionIcon.sprite = Resources.Load<Sprite>($"StatusIcons/{statusCondition}");

        TextMeshProUGUI statusConditionCounter = spawnedStatusEffect.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        statusConditionCounter.text = statusEffects[statusCondition].ToString();
    }

    private void UpdateFigureHP()
    {
        // Updates figure's health
        playerFigureHPSlider.maxValue = localPlayer.playerBattleFigure.totalHealth;
        playerFigureHPSlider.value = localPlayer.playerBattleFigure.currentHealth;
        playerFigureHPText.text = $"HP : {localPlayer.playerBattleFigure.currentHealth}/{localPlayer.playerBattleFigure.totalHealth}";

        enemyFigureHPSlider.maxValue = localPlayer.enemyBattleFigure.totalHealth;
        enemyFigureHPSlider.value = localPlayer.enemyBattleFigure.currentHealth;
        enemyFigureHPText.text = $"HP : {localPlayer.enemyBattleFigure.currentHealth}/{localPlayer.enemyBattleFigure.totalHealth}";
    }

    #region SelectMoveSystem
    private void MoveSelected(Button buttonClicked)
    {
        Debug.Log("Move Selected!");

        // When a move is selected, tells the server which button was clicked
        MoveSelectedServerRpc(buttonClicked.name, NetworkManager.Singleton.LocalClient.PlayerObject);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MoveSelectedServerRpc(string buttonClickedName, NetworkObjectReference playerAttacking)
    {
        Debug.Log("Invoking Move Selected Server Rpc");

        // Grab the local player
        playerAttacking.TryGet(out NetworkObject playerNetworkObject);
        Multiplayer_Player player = playerNetworkObject.GetComponent<Multiplayer_Player>();

        // Determines what move the player selected based on the name of the button that they clicked
        FigurineMove move = MoveClicked(player, buttonClickedName);

        // Check if the move is on cooldown
        if (player.playerBattleFigure.moveCooldowns[move] > 0 || move.moveType == FigurineMove.moveTypes.Passive || move.moveType == FigurineMove.moveTypes.External)
        {
            return;
        }
        OnBattleMoveChosen?.Invoke();

        MoveSelectedClientRpc(move.moveName, playerAttacking);

    }

    [ClientRpc]
    private void MoveSelectedClientRpc(string moveName, NetworkObjectReference playerAttacking)
    {
        Debug.Log("Invoking Move Selected Client Rpc");

        // Grab the player attacking
        playerAttacking.TryGet(out NetworkObject playerNetworkObject);
        Multiplayer_Player player = playerNetworkObject.GetComponent<Multiplayer_Player>();

        // Disables the buttons from the Battle UI if they are the player attacking
        if (localPlayer == player)
        {
            move1Button.transform.parent.gameObject.SetActive(false);
            move2Button.transform.parent.gameObject.SetActive(false);
            move3Button.transform.parent.gameObject.SetActive(false);
        }

        // Sets the player's move to the one given by the server
        player.combatMove = Resources.Load<FigurineMove>($"Moves/{moveName}");
        DisplayPlayerMoveLabel(player.OwnerClientId, moveName);

        // Invokes the BattleMoveChosen Event
        OnBattleMoveChosen?.Invoke();
    }

    private FigurineMove MoveClicked(Multiplayer_Player player, string buttonClickedName)
    {
        Debug.Log("Invoking Selected Move");
        switch (buttonClickedName)
        {
            case "Move1Button":
                player.combatMove = player.playerBattleFigure.move1;

                break;

            case "Move2Button":
                player.combatMove = player.playerBattleFigure.move2;
                break;

            case "Move3Button":
                player.combatMove = player.playerBattleFigure.move3;
                break;

            default:
                break;
        }

        return player.combatMove;
    }
    #endregion

    #region MoveCountdownSystem
    private void StartMoveCountdown(Multiplayer_Player player)
    {
        // Resets the timer
        moveTimer = 10;

        // Starts the Move Countdown Coroutine
        StartCoroutine(StartMoveCountdownCoroutine());
    }

    private IEnumerator StartMoveCountdownCoroutine()
    {
        // Repeats while the timer is greater than 0
        while (moveTimer > 0)
        {
            // Decrements move timer and calls the client rpc
            moveTimer--;
            UpdateMoveTimerClientRpc(moveTimer);

            // Waits before repeatings
            yield return new WaitForSeconds(1f);
        }

        yield return null;
    }

    private void ClearMoveCountdown()
    {
        // Stop timer by setting its value to 0.
        moveTimer = 0;

        // Call Client RPC to clear move countdown for both clients
        ClearTimerClientRpc();
    }

    [ClientRpc]
    private void ClearTimerClientRpc()
    {
        // Clears Timer on Client
        moveTimer = 0;

        // Clears Timer Panel on Battle UI
        timerPanel.SetActive(false);
    }

    [ClientRpc]
    private void UpdateMoveTimerClientRpc(int updatedMoveTimer)
    {
        // Updates all clients move timer to synchronize with the server
        moveTimer = updatedMoveTimer;

        UpdateMoveTimerUI();
    }

    private void UpdateMoveTimerUI()
    {
        moveTimerText.text = moveTimer.ToString();
    }
    #endregion

    private void DisplayPlayerMoveLabel(ulong ownerClientID, string moveName)
    {
        if (ownerClientID == NetworkManager.LocalClientId)
        {
            playerMoveLabel.gameObject.SetActive(true);
            playerMoveDisplayText.text = moveName;
        }
    }

    private void DisplayEnemyMoveLabel(string moveName, Multiplayer_Player attacker)
    {
        if (attacker.OwnerClientId != NetworkManager.LocalClientId)
        {
            enemyMoveLabel.gameObject.SetActive(true);
            enemyMoveDisplayText.text = moveName;
        }
        
    }

    private void EndBattleUI()
    {
        // Resets Move Display
        playerMoveLabel.gameObject.SetActive(false);
        playerMoveDisplayText.text = "";

        enemyMoveLabel.gameObject.SetActive(false);
        enemyMoveDisplayText.text = "";
    }

    private void ToggleBattleUICanvas()
    {
        // Toggles the Battle UI Canvas
        Debug.Log("Toggling Battle UI Canvas");
        Canvas canvas = this.GetComponent<Canvas>();
        canvas.enabled = !canvas.enabled;
    }

    private void ToggleBattleUICanvas(Multiplayer_Player player)
    {
        ToggleBattleUICanvas();
    }
}
