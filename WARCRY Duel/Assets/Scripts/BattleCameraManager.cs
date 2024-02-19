using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleCameraManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Multiplayer_Player.PlayerSceneLoad += DisableBattleCamera;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void DisableBattleCamera(Multiplayer_Player player)
    {
        this.gameObject.GetComponent<Camera>().enabled = false;
        Debug.Log("Disabling Battling Camera!");
    }
}
