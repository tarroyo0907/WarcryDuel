using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq.Expressions;
using Unity.Mathematics;
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
    private const float MoveDelay = 0.1f;
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

        object[] parameters = new object[4] { playerFigurine, enemyFigurine, playerFigurineAttack, enemyFigurineAttack };
        string coroutineName = move.moveName.Replace(" ", "");
        yield return StartCoroutine(coroutineName, parameters);

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

    #region Moves
    IEnumerator Slash(object[] parameters)
    {
        Figurine playerFigurine = (Figurine) parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        enemyFigurine.incomingEffect.IncomingDamage += (int) (playerFigurineAttack * 1.0); 
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator Block(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        playerFigurine.incomingEffect.BlockIncomingDamage = true;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator EmpoweredStrike(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        enemyFigurine.incomingEffect.IncomingDamage += (int)(playerFigurineAttack * 3.0);
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator ShurikenThrow(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        for (int i = 0; i < 3; i++)
        {
            enemyFigurine.incomingEffect.IncomingDamage += (int)(playerFigurineAttack * 0.5);
            enemyFigurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Bleed, 1);

            yield return new WaitForSeconds(0.25f);

            enemyFigurine.TakeEffect();
        }
    }

    IEnumerator Smokebomb(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        playerFigurine.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.Stealth, 2);
        playerFigurine.incomingEffect.BlockIncomingDamage = true;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator EnduringDefense(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        playerFigurine.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.DefenseUp, 3);
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
        
    }

    IEnumerator ImperviousBastion(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        playerFigurine.incomingEffect.BlockIncomingDamage = true;
        playerFigurine.incomingEffect.RemoveAllDebuffs = false;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator RockSwing(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        enemyFigurine.incomingEffect.IncomingDamage += (int)(playerFigurineAttack * 1.0);
        if (UnityEngine.Random.Range(0,5) == 0)
        {
            //enemyFigurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Stunned, 2);
        }
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator Earthquake(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        enemyFigurine.incomingEffect.IncomingDamage += (int)(playerFigurineAttack * 1.5);
        enemyFigurine.incomingEffect.moveEffects.Add(FigurineEffect.MoveEffects.Pushback, 1);
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator ArcaneBlast(object[] parameters)
    {
        Figurine playerFigurine = (Figurine) parameters[0];
        Figurine enemyFigurine = (Figurine) parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        enemyFigurine.incomingEffect.IncomingDamage += (int)(playerFigurineAttack * 1.0);

        if (playerFigurine.ability.abilityName == "Lifesteal")
        {
            playerFigurine.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.Lifesteal, 1);
        }
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator DeceptiveEnergy(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        int lifestealStacks = playerFigurine.buffs[FigurineEffect.StatusEffects.Lifesteal];
        enemyFigurine.incomingEffect.IncomingDamage += ((int)(playerFigurineAttack * 1.0)) * lifestealStacks;
        playerFigurine.incomingEffect.SelfBuffsToRemove.Add(FigurineEffect.StatusEffects.Lifesteal, lifestealStacks);
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator ToxicBud(object[] parameters)
    {
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];
        int playerFigurineAttack = (int)parameters[2];

        enemyFigurine.incomingEffect.IncomingDamage += (int)(playerFigurineAttack * 1.0);
        if (UnityEngine.Random.Range(0, 1) == 0)
        {
            enemyFigurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.DefenseDown, 1);
        }
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    #endregion

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

    #region Abilities
    public void Lifesteal()
    {
        
    }
    #endregion
}
