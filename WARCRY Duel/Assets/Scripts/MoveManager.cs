using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Linq.Expressions;
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
        Debug.Log("CALLING USE MOVE FUNCTION");
        StartCoroutine(StartMoveCoroutine());
    }

    private IEnumerator StartMoveCoroutine()
    {
        yield return new WaitUntil(() => localPlayer.combatMove != null);

        yield return new WaitForSeconds(0.5f);

        OnMoveUse?.Invoke(localPlayer.combatMove.moveName, localPlayer);
        StartCoroutine(UseMoveCoroutine(localPlayer.combatMove, localPlayer.playerBattleFigure, localPlayer.enemyBattleFigure));
    }

    IEnumerator UseMoveCoroutine(FigurineMove move, Figurine playerFigurine, Figurine enemyFigurine)
    {
        Debug.Log("Using Move! : " + move.moveName.ToString());
        object[] parameters = new object[2] { playerFigurine, enemyFigurine };
        string coroutineName = move.moveName.Replace(" ", "");

        yield return StartCoroutine(coroutineName, parameters);

        // Resets Move Cooldown
        playerFigurine.moveCooldowns[move] = move.moveCooldown;
        Debug.Log($"Figurine Move : {move.name}");
        Debug.Log($"Figurine Cooldown : {playerFigurine.moveCooldowns[move]}");

        // Abilities
        CheckForAbilities(move, playerFigurine, enemyFigurine);

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
                    Debug.Log("Applying Lifesteal Ability");
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
        Debug.Log("Slash");
        Figurine playerFigurine = (Figurine) parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        enemyFigurine.incomingEffect.IncomingDamage += 5;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator Block(object[] parameters)
    {
        Debug.Log("Block");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        playerFigurine.incomingEffect.BlockIncomingDamage = true;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator EmpoweredStrike(object[] parameters)
    {
        Debug.Log("Empowered Strike");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        enemyFigurine.incomingEffect.IncomingDamage += 15;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator ShurikenThrow(object[] parameters)
    {
        Debug.Log("Shuriken Throw");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        for (int i = 0; i < 3; i++)
        {
            enemyFigurine.incomingEffect.IncomingDamage += 3;
            enemyFigurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Bleed, 1);

            yield return new WaitForSeconds(0.25f);

            enemyFigurine.TakeEffect();
        }
    }

    IEnumerator Smokebomb(object[] parameters)
    {
        Debug.Log("Smokebomb");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        playerFigurine.incomingEffect.SelfBuffsToApply.Add(FigurineEffect.StatusEffects.Stealth, 3);
        playerFigurine.incomingEffect.BlockIncomingDamage = true;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator ImperviousBastion(object[] parameters)
    {
        Debug.Log("Impervious Bastion");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        playerFigurine.incomingEffect.BlockIncomingDamage = true;
        playerFigurine.incomingEffect.RemoveAllDebuffs = false;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator RockSwing(object[] parameters)
    {
        Debug.Log("Rock Swing");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        enemyFigurine.incomingEffect.IncomingDamage += 4;
        if (Random.Range(0,5) == 0)
        {
            //enemyFigurine.incomingEffect.EnemyDebuffsToApply.Add(FigurineEffect.StatusEffects.Stunned, 2);
        }
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator Earthquake(object[] parameters)
    {
        Debug.Log("Earthquake");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        enemyFigurine.incomingEffect.IncomingDamage += 7;
        enemyFigurine.incomingEffect.moveEffects.Add(FigurineEffect.MoveEffects.Pushback, 1);
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator ArcaneBlast(object[] parameters)
    {
        Debug.Log("ArcaneBlast");
        Figurine playerFigurine = (Figurine) parameters[0];
        Figurine enemyFigurine = (Figurine) parameters[1];

        enemyFigurine.incomingEffect.IncomingDamage += 5;
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    IEnumerator DeceptiveEnergy(object[] parameters)
    {
        Debug.Log("Deceptive Energy");
        Figurine playerFigurine = (Figurine)parameters[0];
        Figurine enemyFigurine = (Figurine)parameters[1];

        int lifestealStacks = playerFigurine.buffs[FigurineEffect.StatusEffects.Lifesteal];
        enemyFigurine.incomingEffect.IncomingDamage += 5 + (5 * lifestealStacks);
        playerFigurine.incomingEffect.SelfBuffsToRemove.Add(FigurineEffect.StatusEffects.Lifesteal, lifestealStacks);
        yield return new WaitForSeconds(MoveDelay);
        enemyFigurine.TakeEffect();
    }

    #endregion

    #region External Moves
    private void UseExternalMove(string selectedExternalMove)
    {
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
        localPlayer.activeExternalMove = selectedExternalMove;
        UseExternalMoveClientRpc(selectedExternalMove);
        
        yield return new WaitUntil(() => waitingForCompletedExternalMove == false);

        Multiplayer_GameManager.Instance.ChangeTurn();
    }

    [ClientRpc]
    private void UseExternalMoveClientRpc(string selectedExternalMove)
    {
        // Announce what external move is being used
        Debug.Log("Using External Move : " + selectedExternalMove);
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
