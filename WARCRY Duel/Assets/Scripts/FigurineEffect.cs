using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FigurineEffect
{
    public enum StatusEffects 
    {
        Bleed,
        Freeze,
        Burn,
        Stealth,
        Stunned
    }

    public enum MoveEffects
    {
        None,
        Pushback
    }

    [SerializeField] public Dictionary<StatusEffects, int> SelfBuffsToApply = new Dictionary<StatusEffects, int>();
    [SerializeField] public Dictionary<StatusEffects, int> SelfDebuffsToApply = new Dictionary<StatusEffects, int>();

    [SerializeField] public Dictionary<StatusEffects, int> EnemyBuffsToApply = new Dictionary<StatusEffects, int>();
    [SerializeField] public Dictionary<StatusEffects, int> EnemyDebuffsToApply = new Dictionary<StatusEffects, int>();

    [SerializeField] public Dictionary<StatusEffects, int> SelfBuffsToRemove = new Dictionary<StatusEffects, int>();
    [SerializeField] public Dictionary<StatusEffects, int> SelfDebuffsToRemove = new Dictionary<StatusEffects, int>();

    [SerializeField] public Dictionary<StatusEffects, int> EnemyBuffsToRemove = new Dictionary<StatusEffects, int>();
    [SerializeField] public Dictionary<StatusEffects, int> EnemyDebuffsToRemove = new Dictionary<StatusEffects, int>();

    [SerializeField] public Dictionary<MoveEffects, int> moveEffects = new Dictionary<MoveEffects, int>();

    [SerializeField] private int incomingDamage = 0;
    [SerializeField] private bool blockIncomingDamage = false;
    [SerializeField] private bool removeAllDebuffs = false;


    public int IncomingDamage { get { return incomingDamage; } set { incomingDamage = value; } }
    public bool BlockIncomingDamage { get { return blockIncomingDamage; } set { blockIncomingDamage = value; } }
    public bool RemoveAllDebuffs { get { return removeAllDebuffs; } set { removeAllDebuffs = value; } }

}
