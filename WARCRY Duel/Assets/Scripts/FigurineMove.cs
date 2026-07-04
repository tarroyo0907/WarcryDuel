using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
// Tyler Arroyo
// Figurine Move
// Scriptable Object for a Figurine's Attacking Move
[CreateAssetMenu(fileName = "New Move", menuName = "Move")]
public class FigurineMove : ScriptableObject
{
    public string moveName;
    public string moveDescription;
    public Sprite moveIcon;
    public int moveCooldown;
    
    public enum moveTypes {Null, Action, Passive, External}
    public moveTypes moveType;
}
