using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

using static Multiplayer_Network;

public class SoundManager : NetworkBehaviour
{
    // References
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip battleMusic;

    [SerializeField] private AudioSource audioSource;

    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(this);

        NetworkManager.Singleton.OnServerStarted += SceneManagerListener;
    }

    // Update is called once per frame
    void Update()
    {

    }

    void SceneManagerListener()
    {
        NetworkManager.SceneManager.OnLoadComplete += StartBattleMusicClientRpc;
    }

    [ClientRpc]
    void StartBattleMusicClientRpc(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        if (sceneName == "Battle" && NetworkManager.LocalClientId == clientId)
        {
            Debug.Log("Play Battle Music");

            audioSource.Stop();
            audioSource.clip = battleMusic;
            audioSource.Play();
        }
    }
}
