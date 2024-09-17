using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Unity.Netcode;

using UnityEngine;

public class CameraManager : MonoBehaviour
{
    // Camera References
    public GameObject player1Camera = null;
    public GameObject player2Camera = null;
    public GameObject player1BattleCamera = null;
    public GameObject player2BattleCamera = null;
    public GameObject player1GoalPerspective = null;
    public GameObject player2GoalPerspective = null;

    private void Awake()
    {
        player1Camera = GameObject.Find("Player 1 Camera");
        player2Camera = GameObject.Find("Player 2 Camera");
        player1BattleCamera = GameObject.Find("Player 1 Battle Camera");
        player2BattleCamera = GameObject.Find("Player 2 Battle Camera");
        player1GoalPerspective = GameObject.Find("Player 1 Goal Perspective");
        player2GoalPerspective = GameObject.Find("Player 2 Goal Perspective");

        Multiplayer_Player.PrepareGame += EnableMainCamera;
        Multiplayer_Player.OnBattleStart += EnableBattleCamera;
        Multiplayer_GameManager.EndCombatEvent += EnableMainCamera;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void EnableMainCamera() 
    {
        Debug.Log("Enabling Main Camera Method");

        switch (NetworkManager.Singleton.LocalClientId)
        {
            case 1:
                if (player1Camera != null)
                {
                    player1Camera.GetComponent<Camera>().enabled = true;
                    player1Camera.GetComponent<AudioListener>().enabled = true;
                }

                if (player1BattleCamera != null)
                {
                    player1BattleCamera.GetComponent<Camera>().enabled = false;
                    player1BattleCamera.GetComponent<AudioListener>().enabled = false;
                }
                
                break;

            case 2:
                if (player2Camera != null) {
                    player2Camera.GetComponent<Camera>().enabled = true;
                    player2Camera.GetComponent<AudioListener>().enabled = true;
                }
                if (player2BattleCamera != null) { 
                    player2BattleCamera.GetComponent<Camera>().enabled = false;
                    player2BattleCamera.GetComponent<AudioListener>().enabled = false;
                }
                break;

            default:
                break;
        }
    }

    public void EnableMainCamera(Multiplayer_Player player)
    {
        EnableMainCamera();
    }

    public void EnableBattleCamera(Multiplayer_Player player)
    {
        Debug.Log("Enabling Battle Camera Method");
        switch (NetworkManager.Singleton.LocalClientId)
        {
            case 1:
                player1Camera.GetComponent<Camera>().enabled = false;
                player1Camera.GetComponent<AudioListener>().enabled = false;
                player1BattleCamera.GetComponent<Camera>().enabled = true;
                player1BattleCamera.GetComponent<AudioListener>().enabled = true;
                break;

            case 2:
                player2Camera.GetComponent<Camera>().enabled = false;
                player2Camera.GetComponent<AudioListener>().enabled = false;
                player2BattleCamera.GetComponent<Camera>().enabled = true;
                player2BattleCamera.GetComponent<AudioListener>().enabled = true;
                break;

            default:
                break;
        }


    }
}
