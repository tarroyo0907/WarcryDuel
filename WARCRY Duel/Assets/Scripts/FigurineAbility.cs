using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Tyler Arroyo
// Figurine Move
// Scriptable Object for a Figurine's Ability
[CreateAssetMenu(fileName = "New Ability", menuName = "Ability")]
public class FigurineAbility : ScriptableObject
{
    public string abilityName;
    public string abilityDescription;
}
