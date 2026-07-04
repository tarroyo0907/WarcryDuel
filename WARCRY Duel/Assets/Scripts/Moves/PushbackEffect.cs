using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "MoveEffects/Pushback")]
public class PushbackEffect : MoveEffect
{
    public override IEnumerator Execute(Multiplayer_Player moveEffectPlayer, Multiplayer_Player attacker, Multiplayer_Player defender)
    {
        List<Tile>[] enemyPossiblePositions = moveEffectPlayer.playerBattleFigure.GetPossiblePositions();
        if (enemyPossiblePositions == null)
        {
            Multiplayer_GameManager.Instance.CancelMoveEffect();
            yield break;
        }

        Multiplayer_GameManager.Instance.activeMoveEffect = this;
        Multiplayer_GameManager.Instance.MoveEffectState = (Multiplayer_GameManager.MoveEffectStateEnum) attacker.playerID;

        Multiplayer_GameManager.Instance.InitiateMoveEffect(name, Multiplayer_GameManager.Instance.MoveEffectState);

        Multiplayer_GameManager.Instance.waitingForCompletedMoveEffect = true;
        yield return new WaitUntil(() => !Multiplayer_GameManager.Instance.waitingForCompletedMoveEffect);
    }

    public override void OnPlayerInteract(GameObject hitObject, Multiplayer_Player player)
    {
        List<Tile>[] enemyPossiblePositions = player.enemyBattleFigure.GetPossiblePositions();
        if (enemyPossiblePositions == null) return;

        foreach (Tile possiblePosition in enemyPossiblePositions[0])
        {
            if (hitObject == possiblePosition.gameObject)
            {
                player.StartCoroutine(player.enemyBattleFigure.MovementSequence(possiblePosition));
                player.FinalizeMoveEffect();
                break;
            }
        }
    }
}
