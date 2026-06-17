using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;
// Tyler Arroyo
// Move Manager
// Manages all the attacking moves a unit can make
public class MoveManager : NetworkBehaviour
{
    #region Delegates
    public delegate void MoveHandler(string move, Multiplayer_Player attacker);
    public delegate void MoveManagerHandler();
    public delegate void ExternalMoveHandler(string moveName);
    #endregion

    #region Events
    public static event MoveHandler OnMoveUse;
    public static event MoveManagerHandler OnMoveEnd;
    public static event ExternalMoveHandler OnUseExternalMove;

    #endregion

    #region Gameplay Fields
    [SerializeField] private Multiplayer_Player localPlayer;
    [SerializeField] private bool waitingForCompletedExternalMove = false;
    #endregion

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Subscribing to GameManager Initiate Moves!");
        Multiplayer_GameManager.InitiateMoves += UseMove;
        localPlayer = this.GetComponent<Multiplayer_Player>();

        Debug.Log("MOVE MANAGER : " + IsOwner);
        if (!IsOwner) { return; }
        PlayerUI.OnPressExternalMoveButton += UseExternalMove;
        Multiplayer_Player.OnCompletedExternalMove += CompletedExternalMove;
    }

    // Update is called once per frame
    void Update()
    {
    }

    private void UseMove()
    {
        StartCoroutine(StartMoveCoroutine());
    }

    private IEnumerator StartMoveCoroutine()
    {
        yield return new WaitUntil(() => localPlayer.combatMove != null);

        yield return new WaitForSeconds(0.5f);

        OnMoveUse?.Invoke(localPlayer.combatMove.moveName, localPlayer);

        try
        {
            StartCoroutine(UseMoveCoroutine(localPlayer.combatMove, localPlayer.playerBattleFigure, localPlayer.enemyBattleFigure));
        }
        catch (System.Exception){}
    }

    IEnumerator UseMoveCoroutine(FigurineMove move, Figurine playerFigurine, Figurine enemyFigurine)
    {
        int playerFigurineAttack = playerFigurine.attackStat;
        int enemyFigurineAttack = playerFigurine.attackStat;

        // Run calculations for all fights
        if (playerFigurine.buffs.ContainsKey(FigurineEffect.StatusEffects.AttackUp))
        {
            Debug.Log("Increased Attack from Attack Up Buff!");
            playerFigurineAttack = (int) (playerFigurineAttack * 1.5);
        }

        yield return StartCoroutine(move.executor.Execute(playerFigurine, enemyFigurine, playerFigurineAttack));

        // Resets Move Cooldown
        playerFigurine.moveCooldowns[move] = move.moveCooldown;

        // Abilities
        // CheckForAbilities(move, playerFigurine, enemyFigurine);

        yield return new WaitForSeconds(1f);

        OnMoveEnd?.Invoke();
    }

    private void CheckForAbilities(FigurineMove move, Figurine playerFigurine, Figurine enemyFigurine)
    {
        if (playerFigurine.ability != null)
        {
            switch (playerFigurine.ability.abilityName)
            {
                case "Lifesteal":
                    // If the figurine uses an action move
                    if (move.moveType == FigurineMove.moveTypes.Action)
                    {
                        // Apply 1 stack of Lifesteal to the Figurine
                        playerFigurine.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.Lifesteal, 2);
                        playerFigurine.TakeEffect();
                    }
                    break;
                default:
                    break;
            }
        }
        
    }

    #region External Moves
    private void UseExternalMove(string selectedExternalMove)
    {
        Debug.Log("USING EXTERNAL MOVE");
        UseExternalMoveServerRpc(selectedExternalMove);
    }

    [ServerRpc]
    private void UseExternalMoveServerRpc(string selectedExternalMove, ServerRpcParams serverRpcParams = default)
    {
        StartCoroutine(ExternalMoveCoroutine(selectedExternalMove));
    }

    IEnumerator ExternalMoveCoroutine(string selectedExternalMove)
    {
        waitingForCompletedExternalMove = true;
        FigurineMove move = Resources.Load<FigurineMove>($"Moves/{selectedExternalMove}");
        localPlayer.SelectedFigurine.moveCooldowns[move] = move.moveCooldown;
        Debug.Log($"External Move : {move.name} has been set to {move.moveCooldown}");
        localPlayer.activeExternalMove = selectedExternalMove;
        UseExternalMoveClientRpc(selectedExternalMove, localPlayer.SelectedFigurine.name);
        
        yield return new WaitUntil(() => waitingForCompletedExternalMove == false);

        Multiplayer_GameManager.Instance.ChangeTurn();
    }

    [ClientRpc]
    private void UseExternalMoveClientRpc(string selectedExternalMove, string figurineName)
    {
        Figurine figurine = GameObject.Find(figurineName).GetComponent<Figurine>();
        FigurineMove move = Resources.Load<FigurineMove>($"Moves/{selectedExternalMove}");
        figurine.moveCooldowns[move] = move.moveCooldown;
        Debug.Log($"External Move : {move.name} has been set to {move.moveCooldown}");
        localPlayer.activeExternalMove = selectedExternalMove;

        OnUseExternalMove?.Invoke(selectedExternalMove);
    }

    private void CompletedExternalMove(Multiplayer_Player player)
    {
        waitingForCompletedExternalMove = true;
        localPlayer.activeExternalMove = "";
    }

    #endregion
}
