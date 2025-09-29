using System.Collections;
using System.Collections.Generic;

using Unity.Netcode;

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using Unity.Services.Matchmaker.Models;

public class PlayerUI : NetworkBehaviour
{
    // Delegates
    public delegate void PlayerUIHandler();
    public delegate void ExternalMoveHandler(string externalMoveName);

    // Events
    public static event PlayerUIHandler OnEndTurn;
    public static event ExternalMoveHandler OnPressExternalMoveButton;

    // Fields
    #region Misc References
    [Header("Misc References")]
    [SerializeField] private Canvas playerCanvas;
    [SerializeField] private TMPro.TextMeshProUGUI TurnIndicatorText;
    [SerializeField] private Button EndTurnButton;
    #endregion

    #region Header References
    [Header("Header References")]
    [SerializeField] private GameObject headerPanel;
    #endregion

    #region Figurine Overview References
    [Header("Figurine Overview References")]
    [SerializeField] private GameObject figurineOverviewPanel;
    [SerializeField] private Image figurineRender;
    #endregion

    #region Figurine Status References
    [Header("Figurine Status References")]
    [SerializeField] private GameObject figurineStatusPanel;
    [SerializeField] private TMPro.TextMeshProUGUI figurineNameText;
    [SerializeField] private Slider figurineHealth;
    [SerializeField] private TMPro.TextMeshProUGUI figurineHealthText;
    [SerializeField] private GameObject buffConditionPanel;
    [SerializeField] private Image buffConditionIcon;
    [SerializeField] private TextMeshProUGUI buffConditionCounterText;
    [SerializeField] private GameObject debuffConditionPanel;
    [SerializeField] private Image debuffConditionIcon;
    [SerializeField] private TextMeshProUGUI debuffConditionCounterText;
    #endregion

    #region Figurine Moveset References
    [Header("Figurine Moveset References")]
    [SerializeField] private GameObject figurineMovesetPanel;
    [SerializeField] private GameObject move1Background;
    [SerializeField] private Button move1Button;
    [SerializeField] private TextMeshProUGUI move1PassiveText;
    [SerializeField] private GameObject move1CooldownPanel;
    [SerializeField] private TextMeshProUGUI move1CooldownCounter;
    [SerializeField] private GameObject move2Background;
    [SerializeField] private Button move2Button;
    [SerializeField] private TextMeshProUGUI move2PassiveText;
    [SerializeField] private GameObject move2CooldownPanel;
    [SerializeField] private TextMeshProUGUI move2CooldownCounter;
    [SerializeField] private GameObject move3Background;
    [SerializeField] private Button move3Button;
    [SerializeField] private TextMeshProUGUI move3PassiveText;
    [SerializeField] private GameObject move3CooldownPanel;
    [SerializeField] private TextMeshProUGUI move3CooldownCounter;
    [SerializeField] private TMPro.TextMeshProUGUI moveDescriptionText;
    [SerializeField] private GameObject externalMoveBackground;
    [SerializeField] private Button externalMoveButton;
    [SerializeField] private TextMeshProUGUI externalMoveText;
    [SerializeField] private Button passiveDisplayButton;
    [SerializeField] private TextMeshProUGUI passiveDisplayButtonText;
    [SerializeField] private TextMeshProUGUI attackStatText;
    [SerializeField] private TextMeshProUGUI defenseStatText;
    #endregion

    #region Data Fields
    // Data Fields
    [SerializeField] private string selectedMoveName;
    [SerializeField] private Figurine UIselectedFigurine;
    #endregion

    private void Awake()
    {
        if (!IsServer)
        {
            // Subscribing to Events
            Multiplayer_GameManager.OnGameStart += ChangeTurnUI;
            Multiplayer_GameManager.OnChangeTurn += ChangeTurnUI;
            Multiplayer_GameManager.EndGameEvent += DisplayWinner;

            Multiplayer_Player.OnBattleStart += TogglePlayerUICanvas;
            Multiplayer_GameManager.EndCombatEvent += TogglePlayerUICanvas;

            Multiplayer_GameManager.OnInitiateMoveEffect += DisablePlayerUICanvas;
            Multiplayer_Player.OnCompletedMoveEffect += EnablePlayerUICanvas;

            Multiplayer_Player.OnCompletedExternalMove += ClearFigurineOverview;

            Multiplayer_Player.SelectOwnFigurine += DisplayFigurineOverview;
            Multiplayer_Player.SelectEnemyFigurine += DisplayFigurineOverview;

            Multiplayer_Player.OnBattleStart += ClearFigurineOverview;
            Multiplayer_Player.OnFigureMoved += ClearFigurineOverview;
            PlayerUI.OnEndTurn += ClearFigurineOverview;

            Multiplayer_Player.OnFigureStartMoving += HideEndTurnButton;
            Multiplayer_Player.OnFigureStopMoving += ShowEndTurnButton;

            // Overview Button Listeners
            move1Button.onClick.AddListener(() => { DisplayMoveDescription(move1Button); });
            move2Button.onClick.AddListener(() => { DisplayMoveDescription(move2Button); });
            move3Button.onClick.AddListener(() => { DisplayMoveDescription(move3Button); });
            passiveDisplayButton.onClick.AddListener(() => { DisplayPassiveDescription(passiveDisplayButton);  });

            // Use External Move Button Listener
            externalMoveButton.onClick.AddListener(() => { OnPressExternalMoveButton?.Invoke(selectedMoveName); });
        }
    }

    #region Base Methods
    // Start is called before the first frame update
    void Start()
    {
        playerCanvas = GetComponent<Canvas>();

        EndTurnButton.onClick.AddListener(() => { OnEndTurnButtonClick(); });

        if (!IsServer)
        {
            // Switches UI to project to the player's camera perspective
            if (NetworkManager.Singleton.LocalClientId == 1)
            {
                playerCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                playerCanvas.worldCamera = GameObject.Find("Player 1 Camera").GetComponent<Camera>();
            }

            if (NetworkManager.Singleton.LocalClientId == 2)
            {
                playerCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                playerCanvas.worldCamera = GameObject.Find("Player 2 Camera").GetComponent<Camera>();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    #endregion

    private void DisplayMoveDescription(Button selectedButton)
    {
        // Grab the move that the player selected
        string path = $"Moves/{selectedButton.name}";
        FigurineMove figurineMove = Resources.Load<FigurineMove>(path);

        // Clear all icons
        move1Background.SetActive(false);
        move2Background.SetActive(false);
        move3Background.SetActive(false);
        attackStatText.gameObject.SetActive(false);
        defenseStatText.gameObject.SetActive(false);

        // Updates move description text and displays it
        selectedMoveName = figurineMove.moveName;
        moveDescriptionText.text = figurineMove.moveDescription;
        moveDescriptionText.gameObject.SetActive(true);

        
        DisplayExternalMove(figurineMove);
    }

    private void DisplayPassiveDescription(Button selectedButton) {
        // Grab the ability that the player selected
        string abilityName = selectedButton.name.Replace(" ", "");
        string path = $"Abilities/{abilityName}";
        FigurineAbility figurineAbility = Resources.Load<FigurineAbility>(path);

        // Clear all icons
        move1Background.SetActive(false);
        move2Background.SetActive(false);
        move3Background.SetActive(false);

        // Updates Description text and displays it
        moveDescriptionText.text = figurineAbility.abilityDescription;
        moveDescriptionText.gameObject.SetActive(true);

    }

    private void DisplayExternalMove(FigurineMove figurineMove)
    {
        Multiplayer_Player localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject.GetComponent<Multiplayer_Player>();
        if (figurineMove.moveType == FigurineMove.moveTypes.External)
        {
            // Checks if the move is usable
            if (localPlayer.SelectedFigurine.moveCooldowns[figurineMove] == 0)
            {
                // Checks if the figure is a part of the player's team
                if (localPlayer.SelectedFigurine.Team == localPlayer.playerTeam)
                {
                    // Checks if it is your turn
                    if (localPlayer.OwnerClientId == (ulong) Multiplayer_GameManager.Instance.GameBattleState)
                    {
                        // Check if the unit isn't in the infirmary
                        string parentName = localPlayer.SelectedFigurine.CurrentSpacePos.transform.parent.gameObject.name;
                        if (parentName != "Player 1 Bench" && parentName != "Player 2 Bench")
                        {
                            // Activate external move background
                            externalMoveBackground.SetActive(true);
                        }
                        
                    }
                }
            }
        }
        else
        {
            externalMoveBackground.SetActive(false);
        }
    }

    private void DisplayFigurineOverview(Multiplayer_Player player)
    {
        Debug.Log("Displaying Figurine Overview!");
        Figurine selectedFigurine = player.SelectedFigurine;
        UIselectedFigurine = selectedFigurine;

        // Enables Figurine Overview Panel
        figurineOverviewPanel.SetActive(true);
        StopCoroutine("CycleBuffCondition");
        StopCoroutine("CycleDebuffCondition");
        externalMoveBackground.SetActive(false);

        // Update Figurine Render
        string selectedFigurineName = selectedFigurine.figurineName;
        string path = $"Figurine Renders/{selectedFigurineName}FigurineRender";
        figurineRender.sprite = Resources.Load<Sprite>(path);
        Debug.Log($"Render Path : {path}");

        // Update Figurine Status Panel
        figurineNameText.text = selectedFigurineName;

        figurineHealth.maxValue = selectedFigurine.totalHealth;
        figurineHealth.value = selectedFigurine.currentHealth;
        figurineHealthText.text = $"{selectedFigurine.currentHealth} / {selectedFigurine.totalHealth}";
        attackStatText.text = $"ATK : {selectedFigurine.attackStat}";
        defenseStatText.text = $"DEF : {selectedFigurine.defenseStat}";

        attackStatText.gameObject.SetActive(true);
        defenseStatText.gameObject.SetActive(true);

        #region Update Move Buttons
        Button[] buttons = new Button[3] { move1Button, move2Button, move3Button };
        FigurineMove[] moveArray = new FigurineMove[3] { selectedFigurine.move1, selectedFigurine.move2, selectedFigurine.move3 };
        GameObject[] moveCooldownPanels = new GameObject[3] { move1CooldownPanel, move2CooldownPanel, move3CooldownPanel };
        TextMeshProUGUI[] moveCooldownCounters = new TextMeshProUGUI[3] { move1CooldownCounter, move2CooldownCounter, move3CooldownCounter };
        GameObject[] moveBackgrounds = new GameObject[3] { move1Background, move2Background, move3Background };
        TextMeshProUGUI[] passiveTextArray = new TextMeshProUGUI[] { move1PassiveText, move2PassiveText, move3PassiveText };
        for (int i = 0; i < moveArray.Length; i++)
        {
            buttons[i].image.sprite = moveArray[i].moveIcon;
            buttons[i].name = moveArray[i].moveName;
            if (moveArray[i].moveType != FigurineMove.moveTypes.Action)
            {
                passiveTextArray[i].gameObject.SetActive(true);
                passiveTextArray[i].text = moveArray[i].moveType.ToString();
            }
            else
            {
                passiveTextArray[i].gameObject.SetActive(false);
            }

            if (selectedFigurine.moveCooldowns[moveArray[i]] > 0)
            {
                moveCooldownPanels[i].SetActive(true);
                moveCooldownCounters[i].text = selectedFigurine.moveCooldowns[moveArray[i]].ToString();
            }
            else
            {
                moveCooldownPanels[i].SetActive(false);
            }
            moveBackgrounds[i].SetActive(true);
        }
        moveDescriptionText.gameObject.SetActive(false);
        #endregion

        #region Updating Status Conditions
        if (selectedFigurine.buffs.Count > 0)
        {
            buffConditionPanel.SetActive(true);
            StartCoroutine(CycleBuffCondition(selectedFigurine));
        }
        else
        {
            buffConditionPanel.SetActive(false);
        }

        if (selectedFigurine.debuffs.Count > 0)
        {
            debuffConditionPanel.SetActive(true);
            StartCoroutine(CycleDebuffCondition(selectedFigurine));
        }
        else
        {
            debuffConditionPanel.SetActive(false);
        }
        #endregion

        #region Passive Check
        // Check for Passive
        if(selectedFigurine.ability == null) {
            passiveDisplayButton.gameObject.SetActive(false);
            
        }
        else
        {
            passiveDisplayButton.gameObject.SetActive(true);
            passiveDisplayButton.name = selectedFigurine.ability.abilityName.ToString();
            passiveDisplayButtonText.text = selectedFigurine.ability.abilityName.ToString();
        }
        #endregion
    }

    private void ClearFigurineOverview()
    {
        // Disables Figurine Overview Panel
        Debug.Log("Clearing Figurine Overview!");
        figurineOverviewPanel.SetActive(false);
        externalMoveBackground.SetActive(false);
        StopCoroutine("CycleBuffCondition");
        StopCoroutine("CycleDebuffCondition");
    }

    private void ClearFigurineOverview(Multiplayer_Player player)
    {
        ClearFigurineOverview();
    }

    #region EndTurnButton
    public void HideEndTurnButton(Multiplayer_Player player)
    {
        EndTurnButton.gameObject.SetActive(false);
    }

    public void ShowEndTurnButton(Multiplayer_Player player)
    {
        EndTurnButton.gameObject.SetActive(true);
    }

    public void OnEndTurnButtonClick()
    {
        EndTurnServerRpc();

        EndTurnButton.gameObject.SetActive(false);
        OnEndTurn?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    private void EndTurnServerRpc(ServerRpcParams serverRpcParams = default)
    {
        OnEndTurn?.Invoke();
    }

    #endregion

    private void ChangeTurnUI()
    {
        ChangeTurnUI(Multiplayer_GameManager.MultiplayerBattleState.START, Multiplayer_GameManager.Instance.GameBattleState);
    }

    private void ChangeTurnUI(Multiplayer_GameManager.MultiplayerBattleState previousState, Multiplayer_GameManager.MultiplayerBattleState newState)
    {
        // Returns from function if the game is over.
        if (newState == Multiplayer_GameManager.MultiplayerBattleState.GAMEOVER)
        {
            Debug.Log("End of Game, disabling button!");
            EndTurnButton.gameObject.SetActive(false);
            return;
        }

        // Checks if it is the player's turn
        if ((ulong) newState == NetworkManager.Singleton.LocalClientId)
        {
            EndTurnButton.gameObject.SetActive(true);
        }
        // If it isn't your turn, disables the End Turn Button
        else
        {
            EndTurnButton.gameObject.SetActive(false);
        }

        // Displays whos turn it is
        StartCoroutine(DisplayTurnIndicator(previousState, newState));
    }

    private IEnumerator DisplayTurnIndicator(Multiplayer_GameManager.MultiplayerBattleState previousState, Multiplayer_GameManager.MultiplayerBattleState newState)
    {
        // If it changes to either Player 1 or Player 2's turn
        if (previousState == Multiplayer_GameManager.MultiplayerBattleState.PLAYERONETURN ||
            previousState == Multiplayer_GameManager.MultiplayerBattleState.PLAYERTWOTURN)
        {
            if ((ulong)previousState != NetworkManager.Singleton.LocalClientId)
            {
                TurnIndicatorText.text = "Your Turn";
                TurnIndicatorText.color = Color.blue;
            }
            else
            {
                TurnIndicatorText.text = "Enemy Turn";
                TurnIndicatorText.color = Color.red;
            }

            TurnIndicatorText.gameObject.SetActive(true);

            yield return new WaitForSeconds(1.5f);

            TurnIndicatorText.gameObject.SetActive(false);
        }

        yield return null;
    }

    private void DisplayWinner()
    {
        // Grabs the Winner
        string Winner = Multiplayer_GameManager.Instance.gameWinner;

        // Checks if this player is the winner
        if (Winner.Contains(NetworkManager.Singleton.LocalClientId.ToString()))
        {
            TurnIndicatorText.text = "You Win!";
            TurnIndicatorText.color = Color.blue;
        }
        // Otherwise if the player lost
        else
        {
            TurnIndicatorText.text = "You Lost!";
            TurnIndicatorText.color = Color.red;
        }

        // Displays the Winner Text Object
        TurnIndicatorText.gameObject.SetActive(true);
    }

    #region PlayerUICanvas
    private void TogglePlayerUICanvas()
    {
        // Toggles the Canvas
        Debug.Log("Toggling Player UI Canvas");
        Canvas canvas = GetComponent<Canvas>();
        canvas.enabled = !canvas.enabled;
    }

    private void TogglePlayerUICanvas(Multiplayer_Player player)
    {
        TogglePlayerUICanvas();
    }

    private void DisablePlayerUICanvas(FigurineEffect.MoveEffects moveEffect)
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.enabled = false;
    }

    private void EnablePlayerUICanvas(Multiplayer_Player player)
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.enabled = true;
    }
    #endregion

    #region Status Effect UI
    IEnumerator CycleBuffCondition(Figurine selectedFigurine)
    {
        int buffIndex = 0;
        while (figurineOverviewPanel.activeSelf)
        {
            FigurineEffect.StatusEffects statusCondition = selectedFigurine.buffs.ElementAt(buffIndex).Key;
            buffConditionIcon.sprite = Resources.Load<Sprite>($"StatusIcons/{statusCondition}");
            buffConditionCounterText.text = selectedFigurine.buffs[statusCondition].ToString();
            buffIndex++;

            if (buffIndex >= selectedFigurine.buffs.Count)
            {
                buffIndex = 0;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator CycleDebuffCondition(Figurine selectedFigurine)
    {
        int debuffIndex = 0;
        while (figurineOverviewPanel.activeSelf)
        {
            FigurineEffect.StatusEffects statusCondition = selectedFigurine.debuffs.ElementAt(debuffIndex).Key;
            debuffConditionIcon.sprite = Resources.Load<Sprite>($"StatusIcons/{statusCondition}");
            debuffConditionCounterText.text = selectedFigurine.debuffs[statusCondition].ToString();
            debuffIndex++;

            if (debuffIndex >= selectedFigurine.debuffs.Count)
            {
                debuffIndex = 0;
            }
            yield return new WaitForSeconds(1f);
        }
    }
    #endregion
}
