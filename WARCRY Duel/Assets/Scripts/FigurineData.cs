using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Figurine", menuName = "Figurine")]
public class FigurineData : ScriptableObject
{
    // Data
    public string figurineName;
    public int currentHealth;
    public int totalHealth;
    public int movementPoints;
    public FigurineMove move1;
    public FigurineMove move2;
    public FigurineMove move3;
}
