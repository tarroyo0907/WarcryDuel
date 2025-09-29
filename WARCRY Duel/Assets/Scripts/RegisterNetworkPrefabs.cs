using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Automatically registers all the network prefabs
/// </summary>
public class RegisterNetworkPrefabs : MonoBehaviour
{
    [SerializeField] private string resourcesPath = "Figurines";

    // Start is called before the first frame update
    void Start()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[AutoRegisterNetworkPrefabs] No NetworkManager.Singleton found.");
            return;
        }

        var prefabs = Resources.LoadAll<GameObject>(resourcesPath);
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"[AutoRegisterNetworkPrefabs] No prefabs found at Resources/{resourcesPath}");
            return;
        }

        int added = 0;
        foreach (var prefab in prefabs)
        {
            if (prefab.GetComponent<NetworkObject>() == null)
            {
                // Skip non-network prefabs
                continue;
            }

            if (prefab.tag == "Figurine")
            {
                networkManager.AddNetworkPrefab(prefab); // <- runtime registration
                added++;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
