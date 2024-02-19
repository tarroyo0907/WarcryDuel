using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleDisc : MonoBehaviour
{
    public GameObject selectedFigurine;
    Dictionary<string, int> moveDict;
    CapsuleCollider discCollider;
    LineRenderer discLR;
    // Start is called before the first frame update
    void Start()
    {
        discLR = gameObject.GetComponent<LineRenderer>();
        discCollider = gameObject.GetComponent<CapsuleCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void CreateMoveBorders()
    {
        foreach (KeyValuePair<string,int> item in moveDict)
        {
            //discLR.SetPositions
        }
    }
}
