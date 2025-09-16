using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;
using Microsoft.Win32.SafeHandles;
using UnityEngine.UI;
using System.Runtime.CompilerServices;
using static Multiplayer_GameManager;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor;
// Tyler Arroyo
// Figurine
// Figurine Object
public class Figurine : NetworkBehaviour
{
    #region Delegates
    public delegate void FigurineHandler(Figurine figure);
    public delegate void StatusEffectHandler(FigurineEffect.StatusEffects statusEffect);
    public delegate void MoveEffectHandler(Figurine sender, KeyValuePair<FigurineEffect.MoveEffects, int> moveEffect);
    #endregion

    #region Events
    public static event FigurineHandler OnStartMoving;
    public static event FigurineHandler OnStopMoving;
    public static event FigurineHandler DefeatedEvent;
    public static event StatusEffectHandler InflictStatusEffectEvent;
    public static event MoveEffectHandler OnApplyMoveEffect;
    #endregion

    #region Data Fields
    [Header("\nData Fields")]
    public string figurineName;
    public int currentHealth;
    public int totalHealth;
    public int movementPoints;
    public bool isSpawnable;
    public bool isAbleToFight;
    public FigurineMove move1;
    public FigurineMove move2;
    public FigurineMove move3;
    public FigurineAbility ability;

    public FigurineEffect incomingEffect = new FigurineEffect();
    public Dictionary<FigurineEffect.StatusEffects, int> buffs = new Dictionary<FigurineEffect.StatusEffects, int>();
    public Dictionary<FigurineEffect.StatusEffects, int> debuffs = new Dictionary<FigurineEffect.StatusEffects, int>();
    #endregion

    #region Gameplay Fields
    [Header("\nGameplay Fields")]
    [SerializeField] private GameObject currentSpacePos;
    [SerializeField] private string team;
    [SerializeField] private List<GameObject> possibleTargets = new List<GameObject>();
    [SerializeField] private List<Tile>[] possiblePositions;

    [SerializeField] private bool isMoving = false;
    [SerializeField] public bool isDefeated = false;

    [SerializeField] private Quaternion originalRotation;

    [SerializeField] public UnityEngine.UI.Slider healthBar;
    [SerializeField] private TMPro.TextMeshProUGUI movementPointText;


    public Dictionary<FigurineMove, int> moveCooldowns;
    #endregion

    #region Properties
    // Properties
    public GameObject CurrentSpacePos { get { return currentSpacePos; } set { currentSpacePos = value; } }
    public List<Tile>[] PossiblePositions { get { return possiblePositions; } set { possiblePositions = value; } }
    public List<GameObject> PossibleTargets { get { return possibleTargets; } set { possibleTargets = value; } }
    public string Team { get { return team; } set { team = value; } }
    #endregion

    #region Base Methods
    // Start is called before the first frame update
    void Start()
    {
        // Update Figurine UI
        try
        {
            // Updating Health Bar
            healthBar.maxValue = totalHealth;
            healthBar.value = currentHealth;

            // Update Movement Points
            movementPointText.text = movementPoints.ToString();
        }
        catch (Exception)
        {
            Debug.Log("No Healthbar or No MovementPoint UI Detected!");
        }

        #region Subscribing to Events
        // Subscribing to Events
        Multiplayer_Player.OnBattleStart += DisableFigurineHealthBar;
        Multiplayer_GameManager.EndCombatEvent += EnableFigurineHealthBar;

        int figureOwnerID = int.Parse(team.Replace("Player ", ""));
        if (NetworkManager.Singleton.LocalClientId == (ulong) figureOwnerID)
        {
            Multiplayer_GameManager.OnChangeTurn += InflictStatusEffect;
        }
        
        Multiplayer_GameManager.OnChangeTurn += ClearPossibleTargets;
        PlayerUI.OnEndTurn += ClearPossibleTargets;
        #endregion

        // Populate Move Cooldowns Dictionary
        try
        {
            moveCooldowns = new Dictionary<FigurineMove, int>();
            moveCooldowns.TryAdd(move1, 0);
            moveCooldowns.TryAdd(move2, 0);
            moveCooldowns.TryAdd(move3, 0);
        }
        catch (ArgumentNullException)
        {
        }

        UpdateGameplayValuesClientRpc(currentSpacePos.name, team, originalRotation);
    }

    // Update is called once per frame
    void Update()
    {

    }
    #endregion

    #region Inflict Status Effect
    public void InflictStatusEffect(MultiplayerBattleState previousState, MultiplayerBattleState newState)
    {
        InflictStatusEffectServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void InflictStatusEffectServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // Inflicts all Buffs
        for (int i = 0; i < buffs.Keys.Count; i++)
        {
            FigurineEffect.StatusEffects statusEffect = buffs.ElementAt(i).Key;

            // Inflict Status Effect On Server
            StartCoroutine(InflictBuffCoroutine((int)statusEffect));

            // Inflict Status Effect on Clients
            InflictBuffClientRpc((int)statusEffect);
        }

        // Inflicts All Debuffs
        for (int i = 0; i < debuffs.Keys.Count; i++)
        {
            Debug.Log("DEBUFF INDEX : " + i);
            FigurineEffect.StatusEffects statusEffect = debuffs.ElementAt(i).Key;

            // Inflict Status Effect On Server
            StartCoroutine(InflictDebuffCoroutine((int)statusEffect));

            // Inflict Status Effect on Clients
            InflictDebuffClientRpc((int)statusEffect);
        }
    }

    [ClientRpc]
    private void InflictBuffClientRpc(int statusEffectEnum)
    {
        StartCoroutine(InflictBuffCoroutine(statusEffectEnum));
    }

    [ClientRpc]
    private void InflictDebuffClientRpc(int statusEffectEnum)
    {
        StartCoroutine(InflictDebuffCoroutine(statusEffectEnum));
    }

    IEnumerator InflictBuffCoroutine(int statusEffectEnum)
    {
        // Inflicts all the status effects that the figurine currently has
        Debug.Log("Inflicting Status Effect on Figurine!");
        FigurineEffect.StatusEffects statusEffect = (FigurineEffect.StatusEffects)statusEffectEnum;
        switch (statusEffect)
        {
            case FigurineEffect.StatusEffects.Stealth:
                break;

            default:
                break;
        }

        // Decrements the Status Effect Stack by 1
        Debug.Log($"Decreasing Status Effect of {statusEffect.ToString()} by 1");
        buffs[statusEffect]--;

        if (buffs[statusEffect] == 0)
        {
            buffs.Remove(statusEffect);
        }

        // Invokes the Status Effect Event and runs on clients side
        InflictStatusEffectEvent?.Invoke(statusEffect);

        yield return new WaitForSeconds(0.5f);

        // Check if the figurine has been defeated due to the status effect
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Defeated();
        }
    }

    IEnumerator InflictDebuffCoroutine(int statusEffectEnum)
    {
        // Inflicts all the status effects that the figurine currently has
        Debug.Log("Inflicting Status Effect on Figurine!");
        FigurineEffect.StatusEffects statusEffect = (FigurineEffect.StatusEffects)statusEffectEnum;
        switch (statusEffect)
        {
            case FigurineEffect.StatusEffects.Bleed:
                // Take Bleed Damage
                currentHealth -= (int)(totalHealth * 0.1);
                healthBar.value = currentHealth;
                break;

            case FigurineEffect.StatusEffects.Freeze:
                break;

            case FigurineEffect.StatusEffects.Burn:
                break;

            default:
                break;
        }

        // Decrements the Status Effect Stack by 1
        Debug.Log($"Decreasing Status Effect of {statusEffect.ToString()} by 1");
        debuffs[statusEffect]--;

        if (debuffs[statusEffect] == 0)
        {
            debuffs.Remove(statusEffect);
        }

        // Invokes the Status Effect Event and runs on clients side
        InflictStatusEffectEvent?.Invoke(statusEffect);

        yield return new WaitForSeconds(0.5f);

        // Check if the figurine has been defeated due to the status effect
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Defeated();
        }
    }
    #endregion

    public void TakeEffect()
    {
        Debug.Log("Figurine Taking Damage");

        // Check for Passive Conditions
        #region Passive Ability Conditions
        FigurineMove[] moveArray = new FigurineMove[3] { move1, move2, move3 };
        foreach (FigurineMove move in moveArray)
        {
            if (move.moveType == FigurineMove.moveTypes.Passive)
            {
                switch (move.moveName)
                {
                    case "Enduring Defense":
                        // Reduces Incoming Damage by 10% rounded up.
                        incomingEffect.IncomingDamage = (int)(incomingEffect.IncomingDamage * 0.9f);
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        // Check for Status Effects
        #region Status Effect Conditions
        if (buffs.ContainsKey(FigurineEffect.StatusEffects.Stealth))
        {
            try
            {
                if (buffs[FigurineEffect.StatusEffects.Stealth] >= 1)
                {
                    incomingEffect.BlockIncomingDamage = true;
                }

            }
            catch (Exception)
            {
                Debug.Log("No Stealth Detected!");
            }

        }
        #endregion

        // If the figure blocks all incoming damage, set incoming damage to 0
        if (incomingEffect.BlockIncomingDamage)
        {
            incomingEffect.IncomingDamage = 0;
            incomingEffect.EnemyDebuffsToApply.Clear();
        }

        // Prevents incoming damage from going below 0
        if (incomingEffect.IncomingDamage < 0)
        {
            incomingEffect.IncomingDamage = 0;
        }

        // Reduces current health by the specified damage
        currentHealth -= incomingEffect.IncomingDamage;
        healthBar.value = currentHealth;
        if (incomingEffect.IncomingDamage != 0)
        {
            StartCoroutine(TakeDamageAnimation());
        }
        

        // Appends the self status effects first
        foreach (KeyValuePair<FigurineEffect.StatusEffects, int> selfBuff in incomingEffect.SelfBuffsToApply)
        {
            try
            {
                buffs.Add(selfBuff.Key, selfBuff.Value);
            }
            catch (Exception)
            {
                buffs[selfBuff.Key] += selfBuff.Value;
            }

        }

        foreach (KeyValuePair<FigurineEffect.StatusEffects, int> selfDebuff in incomingEffect.SelfDebuffsToApply)
        {
            try
            {
                debuffs.Add(selfDebuff.Key, selfDebuff.Value);
            }
            catch (Exception)
            {
                debuffs[selfDebuff.Key] += selfDebuff.Value;
            }

        }

        // Appends the enemies status effects after
        foreach (KeyValuePair<FigurineEffect.StatusEffects, int> enemyBuff in incomingEffect.EnemyBuffsToApply)
        {
            try
            {
                buffs.Add(enemyBuff.Key, enemyBuff.Value);
            }
            catch (Exception)
            {
                buffs[enemyBuff.Key] += enemyBuff.Value;
            }

            Debug.Log($"{figurineName} now has {buffs[enemyBuff.Key]} stacks of {enemyBuff.Key.ToString()}");
        }

        foreach (KeyValuePair<FigurineEffect.StatusEffects, int> enemyDebuff in incomingEffect.EnemyDebuffsToApply)
        {
            try
            {
                debuffs.Add(enemyDebuff.Key, enemyDebuff.Value);
            }
            catch (Exception)
            {
                debuffs[enemyDebuff.Key] += enemyDebuff.Value;
            }

            Debug.Log($"{figurineName} now has {debuffs[enemyDebuff.Key]} stacks of {enemyDebuff.Key.ToString()}");
        }

        // Appends Move Effects Next
        foreach (KeyValuePair<FigurineEffect.MoveEffects, int> incomingMoveEffect in incomingEffect.moveEffects)
        {
            OnApplyMoveEffect?.Invoke(this, incomingMoveEffect);
        }

        // Checks if the figure's health is below 0 and therefore defeated
        if (currentHealth <= 0)
        {
            // Prevents current health from going below 0
            currentHealth = 0;
            Defeated();
        }

        // Resets the incoming effect to it's default state.
        incomingEffect = new FigurineEffect();
    }

    public void Defeated()
    {
        Debug.Log("Figurine Defeated");
        isDefeated = true;
        DefeatedEvent?.Invoke(this);
    }

    public IEnumerator MovementSequence(Tile endingPoint)
    {
        OnStartMoving?.Invoke(this);
        List<Tile> tilePath = new List<Tile>();
        tilePath = FindEndingPoint(currentSpacePos.gameObject.GetComponent<Tile>(), endingPoint, null, 0);
        Debug.Log("Tile Path Count : " + tilePath.Count);
        for (int i = 0; i < tilePath.Count - 1; i++)
        {
            Debug.Log("Tile Path Iteration : " + tilePath[i]);
            StartCoroutine(MoveFigure(tilePath[i], tilePath[i + 1], 1f));
            yield return new WaitUntil(() => isMoving == false);
        }

        Debug.Log("Finishing Movement Sequence");

        // Grabs Possible Targets that the Figurine can attack
        GetPossibleTargets();

        // Checks for Surround Kill
        CheckForSurroundKill();

        // Invokes the Stop Moving Event
        OnStopMoving?.Invoke(this);
        FigureStopMovingClientRpc();
        yield break;
    }

    [ClientRpc]
    private void FigureStopMovingClientRpc()
    {
        // Invokes the Figure Stop Moving Event on both clients
        OnStopMoving.Invoke(this);
    }

    public IEnumerator MoveFigure(Tile startingPoint, Tile endingPoint, float height)
    {
        isMoving = true;
        float timerCount = 0;
        Vector3 startingPos = startingPoint.gameObject.transform.position;
        Vector3 endingPos = endingPoint.gameObject.transform.position;
        Vector3 controlPos = (startingPos + ((endingPos - startingPos) / 2) + Vector3.up * height);
        Vector3 m1 = startingPos;
        Vector3 m2 = endingPos;
        while (timerCount < 1)
        {
            yield return new WaitForEndOfFrame();
            timerCount += 1.5f * Time.deltaTime;
            m1 = Vector3.Lerp(startingPos, controlPos, timerCount);
            m2 = Vector3.Lerp(controlPos, endingPos, timerCount);
            transform.position = Vector3.Lerp(m1, m2, timerCount);
        }
        isMoving = false;
        currentSpacePos = endingPoint.gameObject;
        UpdateFigurePositionClientRpc(currentSpacePos.name);
    }

    [ClientRpc]
    public void UpdateFigurePositionClientRpc(string newSpacePosName)
    {
        currentSpacePos = GameObject.Find(newSpacePosName);
    }

    public List<Tile> FindEndingPoint(Tile startingPoint, Tile endingPoint, Tile previousPoint, int depth)
    {
        // Ends Search Early if Depth is beyond Figure's Movement Points
        if (depth == movementPoints)
        {
            Debug.Log("Figurine - Couldn't Find Ending Point!");
            return new List<Tile>() { };
        }

        foreach (Tile childTile in startingPoint.AccessibleTiles)
        {
            if (previousPoint != null)
            {
                if (childTile.Equals(previousPoint))
                {
                    continue;
                }
            }

            Debug.Log($"Child Tile : {childTile}");
            if (childTile.Equals(endingPoint))
            {
                List<Tile> tiles = new List<Tile> { startingPoint, endingPoint };
                return tiles;
            }
            else
            {
                List<Tile> tiles = FindEndingPoint(childTile, endingPoint, startingPoint, depth + 1);

                if (tiles == null || tiles.Count == 0)
                {
                    continue;
                }

                tiles.Insert(0, startingPoint);
                return tiles;
            }
        }

        return null;
    }

    public List<Tile>[] GetPossiblePositions()
    {
        if (movementPoints == 0)
        {
            return null;
        }

        // Sets the size of the array to the number of movement points
        possiblePositions = new List<Tile>[movementPoints];

        // Creates an empty list for each index of Possible Positions
        for (int i = 0; i < possiblePositions.Length; i++)
        {
            possiblePositions[i] = new List<Tile>();
        }

        // Assigns the first index of Possible Positions to the tiles that connect to the figure's current spot
        Tile parentTile = null;
        try
        {
            parentTile = currentSpacePos.GetComponent<Tile>();
        }
        catch (System.Exception)
        {
            Debug.Log($"Couldn't grab the Tile Component of {currentSpacePos.name}");
        }

        // Grabs all the figurines on the board
        GameObject[] Figurines = GameObject.FindGameObjectsWithTag("Figurine");

        // Checks each accessible tile connecting to the parent tile
        int accessibleTileCount = 0;
        foreach (Tile accessibileTile in parentTile.AccessibleTiles)
        {
            bool IsAccessibleTile = true;
            foreach (GameObject figure in Figurines)
            {
                // Check if any figurine is standing on top of an accessible tile
                if (figure.GetComponent<Figurine>().CurrentSpacePos.name == accessibileTile.name)
                {
                    // Sets the tile's accessibility to false
                    IsAccessibleTile = false;
                    break;
                }
            }

            // If it truly is an Accessible Tile, append it
            if (IsAccessibleTile)
            {
                possiblePositions[0].Add(accessibileTile);
                accessibleTileCount++;
            }
        }

        // If there are no accessible tiles, return null
        if (accessibleTileCount == 0)
        {
            return null;
        }


        // Depending on the movement points, keeps adding to the list of possible positions
        // Each Index represents the depth
        for (int i = 0; i < movementPoints - 1; i++)
        {
            foreach (Tile accessibleTile in possiblePositions[i])
            {
                foreach (Tile childTile in accessibleTile.AccessibleTiles)
                {
                    // Adds the tile only if the previous index doesn't contain it
                    if (!possiblePositions[i].Contains(childTile))
                    {
                        bool IsAccessibleTile = true;
                        foreach (GameObject figure in Figurines)
                        {

                            // Check if any figurine is standing on top of an accessible tile
                            if (figure.GetComponent<Figurine>().CurrentSpacePos.name == childTile.name)
                            {
                                // Sets the tile's accessibility to false
                                IsAccessibleTile = false;
                                break;
                            }
                        }

                        // If it truly is an Accessible Tile, append it
                        if (IsAccessibleTile)
                        {
                            possiblePositions[i + 1].Add(childTile);
                        }

                    }
                }
            }
        }


        return possiblePositions;

    }

    public List<GameObject> GetPossibleTargets()
    {
        Debug.Log("FINDING POSSIBLE TARGETS");
        List<GameObject> possibleTargets = new List<GameObject>();

        // Grabs the Tile Component of this figurine's current space
        Tile currentTile = null;
        try
        {
            currentTile = currentSpacePos.GetComponent<Tile>();
        }
        catch (System.Exception)
        {
            Debug.Log($"Couldn't grab the Tile Component of {currentSpacePos.name}");
        }

        // Grabs all the figurines on the board
        GameObject[] Figurines = GameObject.FindGameObjectsWithTag("Figurine");

        // Checks each accessible tile connecting to the parent tile
        foreach (Tile possibleTargetTile in currentTile.AccessibleTiles)
        {
            foreach (GameObject figure in Figurines)
            {
                // Checks if the figure is an enemy
                if (figure.GetComponent<Figurine>().team != this.team)
                {
                    if (figure.GetComponent<Figurine>().CurrentSpacePos.name == possibleTargetTile.name)
                    {
                        if (figure.GetComponent<Figurine>().isDefeated == false)
                        {
                            // Adds figure as a possible target
                            possibleTargets.Add(figure);
                        }
                    }
                }
            }
        }

        // Returns list of possible targets
        Debug.Log(string.Join(", ", possibleTargets));
        this.possibleTargets = possibleTargets;
        return possibleTargets;

    }

    public void CheckForSurroundKill()
    {
        Debug.Log("Checking for Surround Kill");
        List<GameObject> surroundingEnemies = GetPossibleTargets();
        List<GameObject> defeatedEnemies = new List<GameObject>();
        foreach (GameObject enemy in surroundingEnemies)
        {
            Figurine enemyFigurine = enemy.GetComponent<Figurine>();
            if (enemyFigurine.GetPossibleTargets().Count == enemyFigurine.currentSpacePos.GetComponent<Tile>().AccessibleTiles.Count)
            {
                enemyFigurine.Defeated();
                defeatedEnemies.Add(enemy);
            }
        }

        foreach (GameObject enemy in defeatedEnemies)
        {
            this.possibleTargets.Remove(enemy);
        }


    }

    public void ActivateAbility()
    {
        // Checks if the figurine has an ability
        if (ability != null)
        {
            // Check what ability the figurine has an activate it
            switch (ability.name)
            {
                case "Lifesteal":

                    break;
                default:
                    break;

            }
        }
    }

    public IEnumerator TakeDamageAnimation()
    {
        // Get MeshRenderer Component
        SkinnedMeshRenderer figureMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        if (figureMeshRenderer == null)
        {
            yield break;
        }

        // Store original color before flashing red
        Color originalColor = figureMeshRenderer.material.color;
        figureMeshRenderer.material.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        figureMeshRenderer.material.color = originalColor;
    }

    #region Helper Methods
    public void StoreOriginalAlignment()
    {
        // Stores its original alignment for later reference
        originalRotation = transform.rotation;
    }

    public void ReturnToOriginalAlignment()
    {
        // Returns its rotation to its original alignment
        transform.rotation = originalRotation;
    }

    public void EnableFigurineHealthBar(Multiplayer_Player player)
    {
        EnableFigurineHealthBar();
    }

    public void EnableFigurineHealthBar()
    {
        if (healthBar != null)
        healthBar.gameObject.SetActive(true);
    }

    public void DisableFigurineHealthBar(Multiplayer_Player player)
    {
        DisableFigurineHealthBar();
    }

    public void DisableFigurineHealthBar()
    {
        if (healthBar != null)
        healthBar.gameObject.SetActive(false);
    }

    [ClientRpc]
    public void UpdateGameplayValuesClientRpc(string currentSpacePos, string teamName, Quaternion originalRotation)
    {
        this.currentSpacePos = GameObject.Find(currentSpacePos);
        this.team = teamName;
        this.originalRotation = originalRotation;
    }

    public void ClearPossibleTargets(MultiplayerBattleState previousState, MultiplayerBattleState newState)
    {
        ClearPossibleTargets();
    }

    public void ClearPossibleTargets()
    {
        possibleTargets.Clear();
    }

    #endregion
}
