using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.Netcode;
using Unity.Services.Matchmaker.Models;


using UnityEngine;
using UnityEngine.Android;

public class PFXManager : NetworkBehaviour
{
    #region Fields
    [Header("Particle Effects")]
    [SerializeField] private GameObject ringEffect;
    [SerializeField] private GameObject highlightedEffect;
    [SerializeField] private GameObject boardSpaceEffect;
    [SerializeField] private GameObject enemyHighlightedEffect;
    [SerializeField] private float ringHeight;
    #endregion

    #region Base Methods
    // Start is called before the first frame update
    void Start()
    {
        Multiplayer_GameManager.OnChangeTurn += ClearFigureSelectParticles;
        Multiplayer_GameManager.OnChangeTurn += SelectFigureParticles;

        Multiplayer_GameManager.OnInitiateMoveEffect += HandleMoveEffectParticles;

        MoveManager.OnUseExternalMove += HandleExternalMoveParticles;
        MoveManager.OnMoveUse += HandleMoveVFX;

        TeamSpawner.OnFiguresSpawned += SelectFigureParticles;

        Multiplayer_Player.SelectOwnFigurine += ClearFigureSelectParticles;
        Multiplayer_Player.SelectOwnFigurine += CreateSelectedFigureParticles;
        Multiplayer_Player.SelectOwnFigurine += ShowPossibleFigurePositions;
        Multiplayer_Player.FindingPossibleTargets += ShowPossibleTargets;

        Multiplayer_Player.SelectEnemyFigurine += ClearFigureSelectParticles;
        Multiplayer_Player.SelectEnemyFigurine += CreateSelectedFigureParticles;

        Multiplayer_Player.OnFigureStartMoving += ClearFigureSelectParticles;
        Multiplayer_Player.OnBattleStart += ClearFigureSelectParticles;

        Multiplayer_Player.OnFigureMoved += ShowPossibleTargets;
        Figurine.OnStopMoving += ShowPossibleTargets;
        Figurine.OnStartMoving += ClearFigureSelectParticles;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    #endregion

    #region Clear Particles
    private void ClearFigureSelectParticles(Multiplayer_GameManager.MultiplayerBattleState previousState, Multiplayer_GameManager.MultiplayerBattleState newState)
    {
        ClearFigureSelectParticles();
    }

    private void ClearFigureSelectParticles(object gameObject)
    {
        ClearFigureSelectParticles();
    }

    private void ClearFigureSelectParticles()
    {
        // Clears all existing particle effects
        GameObject[] ringEffects = GameObject.FindGameObjectsWithTag("RingEffect");
        List<GameObject> PFXEffects = new List<GameObject>(ringEffects);
        PFXEffects.AddRange(GameObject.FindGameObjectsWithTag("HighlightedEffect"));
        PFXEffects.AddRange(GameObject.FindGameObjectsWithTag("BoardSpaceEffect"));
        foreach (GameObject item in PFXEffects)
        {
            Destroy(item);
        }
    }
    #endregion

    private void CreateSelectedFigureParticles(Multiplayer_Player player)
    {
        Debug.Log("Creating Selected Figure Particles!");
        // Creates new particle effects
        Vector3 particlePos = player.SelectedFigurine.transform.position;
        particlePos.y = particlePos.y + ringHeight;
        Instantiate(highlightedEffect, particlePos, highlightedEffect.transform.rotation);
    }
    private void SelectFigureParticles()
    {
        if ((int)Multiplayer_GameManager.Instance.GameBattleState == (int)NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Displaying Select Team Figure Particles!!!");
            Multiplayer_Player player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject.GetComponent<Multiplayer_Player>();
            for (int i = 0; i < player.PlayerTeamUnits.Count; i++)
            {
                GameObject figure = player.PlayerTeamUnits[i];
                Transform figureTransform = figure.transform;
                Vector3 ringEffectPos = figureTransform.transform.position;
                ringEffectPos.y = ringEffectPos.y + ringHeight;
                Instantiate(ringEffect, ringEffectPos, ringEffect.transform.rotation, figureTransform);
            }
        }
    }
    private void SelectFigureParticles(Multiplayer_GameManager.MultiplayerBattleState previousState, Multiplayer_GameManager.MultiplayerBattleState newState)
    {
        SelectFigureParticles();
    }
    private void CreateFigureTeamParticles()
    {
        Multiplayer_GameManager gameManager = Multiplayer_GameManager.Instance;
        Debug.Log("Game State Value : " + (int) gameManager.GameBattleState);
        ulong clientID = 0;
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
{
                TargetClientIds = new ulong[] { clientID }
            }
        };

        // If it is player one's turn, send clientRpc to Player One
        if ((int) gameManager.GameBattleState == 1)
        {
            clientID = 1;
            clientRpcParams.Send.TargetClientIds = new ulong[] { clientID };
        }

        // If it is Player Two's turn, send clientRpc to Player Two
        if ((int)gameManager.GameBattleState == 2)
        {
            clientID = 2;
            clientRpcParams.Send.TargetClientIds = new ulong[] { clientID };
        }

        SelectFigureParticlesClientRpc(clientRpcParams);
    }

    [ClientRpc]
    private void SelectFigureParticlesClientRpc(ClientRpcParams clientRpcParams = default)
    {
        Debug.Log("Displaying Select Figure Particles!");
        Multiplayer_Player player = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Multiplayer_Player>();
        Debug.Log("Team Count : " + player.PlayerTeamUnits.Count);
        for (int i = 0; i < player.PlayerTeamUnits.Count; i++)
{
            GameObject figure = player.PlayerTeamUnits[i];
            Transform figureTransform = figure.transform;
            Vector3 ringEffectPos = figureTransform.transform.position;
            ringEffectPos.y = ringEffectPos.y + ringHeight;
            Instantiate(ringEffect, ringEffectPos, ringEffect.transform.rotation);
        }
    }

    #region Show Possible Targets
    private void ShowPossibleTargets(Multiplayer_Player player)
    {
        // Calls the base function by passing in the player's selected figurine
        ShowPossibleTargets(player.SelectedFigurine);
    }

    private void ShowPossibleTargets(Figurine selectedFigurine)
    {
        // Checks if the player can still attack this turn
        if (Multiplayer_GameManager.Instance.CanAttack)
        {
            // Iterates through each possible target of the selected figurine
            Debug.Log("Showing Possible Targets!");
            foreach (GameObject figurine in selectedFigurine.PossibleTargets)
            {
                // Creates the particle effect
                Vector3 particlePos = figurine.transform.position;
                BoxCollider figureCollider = figurine.GetComponent<BoxCollider>();
                particlePos.y = figurine.transform.position.y + figureCollider.bounds.size.y;
                Instantiate(enemyHighlightedEffect, particlePos, enemyHighlightedEffect.transform.rotation, figurine.transform);
            }
        }
    }
    #endregion

    #region Show Possible Positions
    private void ShowPossibleFigurePositions(Multiplayer_Player player)
    {
        if (player.IsHighlightingPositions)
        {
            ShowPossiblePositionsServerRpc();
        }
        
    }
        

    [ServerRpc(RequireOwnership = false)]
    private void ShowPossiblePositionsServerRpc(ServerRpcParams serverRpcParams = default)
    {
        Debug.Log("Showing Possible Figure Positions Server Rpc!");
        ulong playerID = serverRpcParams.Receive.SenderClientId;
        Multiplayer_Player player = NetworkManager.Singleton.ConnectedClients[playerID].PlayerObject.GetComponent<Multiplayer_Player>();
        Figurine selectedFigurine = player.SelectedFigurine;

        List<string> possiblePositionNamesList = new List<string>();
        foreach (List<Tile> positions in selectedFigurine.PossiblePositions)
        {
            foreach (Tile tile in positions)
            {
                possiblePositionNamesList.Add(tile.name);
            }
        }
        string[] possiblePositionNames = possiblePositionNamesList.ToArray();
        StringContainer[] possiblePositions = new StringContainer[possiblePositionNames.Length];
        for (int i = 0; i < possiblePositionNames.Length; i++)
        {
            possiblePositions[i] = new StringContainer { Text = possiblePositionNames[i] };
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId } }
        };

        ShowPossiblePositionsClientRpc(possiblePositions, clientRpcParams);
    }

    [ClientRpc]
    private void ShowPossiblePositionsClientRpc(StringContainer[] possiblePositionNames, ClientRpcParams clientRpcParams)
    {
        Debug.Log("Showing Possible Positions for Figurine Client Rpc!");
        foreach (StringContainer name in possiblePositionNames)
        {
            Tile tile = GameObject.Find(name.Text).GetComponent<Tile>();
            Vector3 particlePos = tile.gameObject.transform.position;
            Instantiate(boardSpaceEffect, particlePos, boardSpaceEffect.transform.rotation, tile.gameObject.transform);
        }
    }
    #endregion

    private void HandleMoveVFX(string moveName, Multiplayer_Player user)
    {
        // Call the function that corresponds to the used move
        moveName = moveName.Replace(" ","");
        try
        {
            StartCoroutine(moveName + "VFX", user);
        }
        catch (Exception){}
        
    }

    #region MoveVFX
    private IEnumerator SlashVFX(Multiplayer_Player user) 
    {
        // Load in Slash VFX
        GameObject slashVFX = Resources.Load<GameObject>("Effects/VFX_Slash");

        // Spawn in Slash VFX
        Vector3 enemyForwardVector = user.enemyBattleFigure.transform.forward;
        Vector3 enemyPosition = user.enemyBattleFigure.transform.position;
        Quaternion playerRotation = user.playerBattleFigure.transform.rotation;
        GameObject slashEffect = Instantiate(slashVFX, enemyPosition + (enemyForwardVector * 0.75f) + new Vector3(0, 0.5f, 0), playerRotation);
        return null;
    }

    private IEnumerator SmokebombVFX(Multiplayer_Player user)
    {
        // Load in Smokebomb VFX
        GameObject smokebombVFX = Resources.Load<GameObject>("PFX/SmokeEffect");

        // Spawn in SmokeBomb VFX
        Vector3 playerPosition = user.playerBattleFigure.transform.position;
        Quaternion playerRotation = user.playerBattleFigure.transform.rotation;
        GameObject smokeBombEffect = Instantiate(smokebombVFX, playerPosition, smokebombVFX.transform.rotation);
        return null;
    }

    private IEnumerator ArcaneBlastVFX(Multiplayer_Player user)
    {
        // Load in Arcane Blast PFX
        GameObject arcaneBlastPFX = Resources.Load<GameObject>("PFX/PS_ArcaneBlast");

        // Spawn in Arcane Blast
        Vector3 playerPosition = user.playerBattleFigure.transform.position;
        Quaternion playerRotation = user.playerBattleFigure.transform.rotation;
        GameObject arcaneBlastEffect = Instantiate(arcaneBlastPFX, playerPosition, arcaneBlastPFX.transform.rotation);

        yield return new WaitForSeconds(3f);

        Destroy(arcaneBlastEffect);
    }
    #endregion

    private void HandleMoveEffectParticles(FigurineEffect.MoveEffects moveEffect)
    {
        Multiplayer_Player activePlayer = null;
        if (Multiplayer_GameManager.Instance.MoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERONE)
        {
            activePlayer = Multiplayer_GameManager.Instance.player1;
        }
        else if (Multiplayer_GameManager.Instance.MoveEffectState == Multiplayer_GameManager.MoveEffectStateEnum.PLAYERTWO)
        {
            activePlayer = Multiplayer_GameManager.Instance.player2;
        }

        // Check which Move Effect is being initiated
        switch (moveEffect)
        {
            case FigurineEffect.MoveEffects.Pushback:
                if (NetworkManager.Singleton.LocalClient.PlayerObject.gameObject.GetComponent<Multiplayer_Player>() == activePlayer)
                {
                    List<Tile>[] possiblePositions = activePlayer.enemyBattleFigure.GetPossiblePositions();
                    foreach (Tile boardSpace in possiblePositions[0])
                    {
                        Vector3 particlePos = boardSpace.gameObject.transform.position;
                        Instantiate(boardSpaceEffect, particlePos, boardSpaceEffect.transform.rotation, boardSpace.gameObject.transform);
                    }
                }
                else
                {
                    return;
                }

                break;
            default:
                break;
        }
    }

    private void HandleExternalMoveParticles(string externalMoveName)
    {
        ClearFigureSelectParticles();
        Multiplayer_Player localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject.GetComponent<Multiplayer_Player>();
        switch (externalMoveName)
        {
            case "Fortification":
                // Only Display if it's the local player's turn
                if (localPlayer.OwnerClientId == (ulong) Multiplayer_GameManager.Instance.GameBattleState)
                {
                    List<Tile>[] possiblePositions = localPlayer.SelectedFigurine.GetPossiblePositions();
                    foreach (Tile boardSpace in possiblePositions[0])
                    {
                        Vector3 particlePos = boardSpace.gameObject.transform.position;
                        Instantiate(boardSpaceEffect, particlePos, boardSpaceEffect.transform.rotation, boardSpace.gameObject.transform);
                    }
                }
                break;
            default:
                break;
        }
    }

    public class StringContainer : INetworkSerializable
    {
        public string Text;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsWriter)
            {
                serializer.GetFastBufferWriter().WriteValueSafe(Text);
            }
            else
            {
                serializer.GetFastBufferReader().ReadValueSafe(out Text);
            }
        }
    }
}
