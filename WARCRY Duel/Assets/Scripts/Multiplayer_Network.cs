
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Matchmaker.PayloadProxy;
using Unity.Services.Matchmaker.Http;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine.SceneManagement;

// Tyler Arroyo
// Multiplayer Network
// Handles the multiplayer network
public class Multiplayer_Network : NetworkBehaviour
{
    // Fields
    [SerializeField] private bool updateScene;
    [SerializeField] private GameObject gameManager;
    [SerializeField] private int connectedClients = 0;
    [SerializeField] private string battleScene;


    // Start is called before the first frame update
    async void Start()
    {
        // Starts Server and updates scene for all players

    }

    // Update is called once per frame
    async void Update()
    {

    }





    
}
