using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    // Fields
    [SerializeField] private List<Tile> accessibleTiles = new List<Tile> ();
    [SerializeField] private SetupBoard board;

    // Properties
    public List<Tile> AccessibleTiles { get { return accessibleTiles; } }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
