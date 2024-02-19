
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services;
using Unity.Services.Core;
using Unity.Services.Multiplay;
using Unity.Services.Multiplay.Models;
using Unity.Services.Matchmaker;
using Unity.Services.Authentication;
using Unity.Services.Matchmaker.Models;
using Unity.Netcode.Transports.UTP;
using static Multiplayer_Network;

// Tyler Arroyo
// Main Menu Manager
// Manages the buttons in the Main Menu
public class MainMenuManager : NetworkBehaviour
{

    // Fields
    [SerializeField] private Button battleButton;
    [SerializeField] private Button partyButton;
    [SerializeField] private Button startServerButton;

    private CreateTicketResponse createTicketResponse;
    [SerializeField] private float pollTicketTimerMax = 1.1f;
    [SerializeField] private float pollTicketTimer;

#if DEDICATED_SERVER
    private float autoAllocateTimer = 9999999f;
    private bool alreadyAutoAllocated;
    private static IServerQueryHandler serverQueryHandler;
    
    private string backfillTicketId;
    private float acceptBackfillTicketsTimer;
    private float acceptBackfillTicketsTimerMax = 1.1f;

    private bool battleStarting = false;
#endif

    private async void Awake()
    {
        InitializeUnityAuthentication();

        pollTicketTimer = pollTicketTimerMax;
        createTicketResponse = null;

        DontDestroyOnLoad(this);

        NetworkManager.Singleton.OnServerStarted += LocalServerTesting;

#if DEDICATED_SERVER
        backfillTicketId = null;
        serverQueryHandler = null;
        var serviceConfig = MultiplayService.Instance.ServerConfig;
        ushort port = serviceConfig.Port;
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("0.0.0.0", port, "0.0.0.0");

        // Handles Backfill Tickets
        //SetupBackfillTickets();

        Debug.Log("Starting as Server!");
        NetworkManager.Singleton.StartServer();
        await MultiplayService.Instance.ReadyServerForPlayersAsync();
#endif
    }

    // Start is called before the first frame update
    void Start()
    {
        if (!IsServer)
        {
            //Screen.SetResolution(450, 950, false);
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }

        battleButton.onClick.AddListener(() =>
        {
            FindLocalMatch();

        });

        startServerButton.onClick.AddListener(() => { StartLocalServer(); });
    }

    // Update is called once per frame
    async void Update()
    {
        if (Screen.width > (Screen.height / 19) * 9)
        {
            Screen.SetResolution((Screen.height / 19) * 9, Screen.height, FullScreenMode.Windowed);
        }
        if (createTicketResponse != null)
        {
            //Has Ticket
            pollTicketTimer -= Time.deltaTime;
            if (pollTicketTimer <= 0f)
            {
                pollTicketTimer = pollTicketTimerMax;

                PollMatchmakerTicket();
            }
        }

#if DEDICATED_SERVER
        if (serverQueryHandler != null)
        {
            serverQueryHandler.UpdateServerCheck();
        }

        if (backfillTicketId != null)
        {
            acceptBackfillTicketsTimer -= Time.deltaTime;
            if (acceptBackfillTicketsTimer <= 0f)
            {
                acceptBackfillTicketsTimer = acceptBackfillTicketsTimerMax;
                HandleBackfillTickets();
            }
        }
#endif

    }

    #region Local Match Functionality
    private async void FindLocalMatch()
    {
        Debug.Log("Finding LOCAL Match...");

        NetworkManager.Singleton.StartClient();
    }

    private void LocalServerTesting()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += ChangeNetworkScene;

            if (SceneManager.GetActiveScene().name == "MainMenu")
            {
                NetworkManager.SceneManager.LoadScene("Loading", LoadSceneMode.Single);
            }
        }

    }

    public void StartLocalServer()
    {
        NetworkManager.StartServer();
    }

    #endregion
    private async void FindMatch()
    {
        Debug.Log("Finding Match...");

        // Creates Matchmaker Ticket
        createTicketResponse = await MatchmakerService.Instance.CreateTicketAsync(new List<Unity.Services.Matchmaker.Models.Player>
        {
            new Unity.Services.Matchmaker.Models.Player(AuthenticationService.Instance.PlayerId),
        }, new CreateTicketOptions { QueueName = "ranked-queue" });
    }

    private async void InitializeUnityAuthentication()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions initializationOptions = new InitializationOptions();

#if !DEDICATED_SERVER
            initializationOptions.SetProfile(UnityEngine.Random.Range(0, 10000).ToString());
#endif

            await UnityServices.InitializeAsync(initializationOptions);

#if !DEDICATED_SERVER
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Signed In Anonymously");
#endif

#if DEDICATED_SERVER
            Debug.Log("Dedicated_Server Lobby");

            MultiplayEventCallbacks multiplayEventCallbacks = new MultiplayEventCallbacks();
            multiplayEventCallbacks.Allocate += MultiplayEventCallbacks_Allocate;
            multiplayEventCallbacks.Deallocate += MultiplayEventCallbacks_Deallocate;
            multiplayEventCallbacks.Error += MultiplayEventCallbacks_Error;
            multiplayEventCallbacks.SubscriptionStateChanged += MultiplayEventCallbacks_SubscriptionStateChanged;
            IServerEvents serverEvents = await MultiplayService.Instance.SubscribeToServerEventsAsync(multiplayEventCallbacks);

            IServerQueryHandler serverQueryHandler = await MultiplayService.Instance.StartServerQueryHandlerAsync(2, "Ranked Match", "WARCRY DUEL", "1.0", "Default");

            var serverConfig = MultiplayService.Instance.ServerConfig;
            if (serverConfig.AllocationId != "")
            {
                // Already Allocated
                MultiplayEventCallbacks_Allocate(new MultiplayAllocation("", serverConfig.ServerId, serverConfig.AllocationId));
            }
#endif
        }
        else
        {
#if DEDICATED_SERVER
            // Already Initialized
            var serverConfig = MultiplayService.Instance.ServerConfig;
            if (serverConfig.AllocationId != "")
            {
                // Already Allocated
                MultiplayEventCallbacks_Allocate(new MultiplayAllocation("", serverConfig.ServerId, serverConfig.AllocationId));
            }
#endif
        }
    }

    private async void PollMatchmakerTicket()
    {
        Debug.Log("PollMatchmakerTicket");

        // Returns the status of the client's ticket
        TicketStatusResponse ticketStatusResponse = await MatchmakerService.Instance.GetTicketAsync(createTicketResponse.Id);

        if (ticketStatusResponse == null)
        {
            // Null means no updates to this ticket, keep waiting
            Debug.Log("Null means no updates to this ticket, keep waiting");
            return;
        }

        // Not null means there is an update to the ticket
        if (ticketStatusResponse.Type == typeof(MultiplayAssignment))
        {
            // It's a Multiplay Assignment
            MultiplayAssignment multiplayAssignment = ticketStatusResponse.Value as MultiplayAssignment;

            Debug.Log("multiplayAssignment.Status " + multiplayAssignment.Status);
            switch (multiplayAssignment.Status)
            {
                case MultiplayAssignment.StatusOptions.Timeout:
                    createTicketResponse = null;
                    Debug.Log("Multiplay Timeout!");
                    break;

                case MultiplayAssignment.StatusOptions.Failed:
                    createTicketResponse = null;
                    Debug.Log("Failed to create Multiplay server!");
                    break;

                case MultiplayAssignment.StatusOptions.InProgress:
                    // Still waiting...
                    break;

                case MultiplayAssignment.StatusOptions.Found:
                    createTicketResponse = null;

                    Debug.Log(multiplayAssignment.Ip + " " + multiplayAssignment.Port);

                    string ipv4Address = multiplayAssignment.Ip;
                    ushort port = (ushort)multiplayAssignment.Port;
                    NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipv4Address, port);

                    Debug.Log("Starting as Client!");
                    NetworkManager.Singleton.StartClient();
                    break;

                default:
                    break;
            }
        }
    }

    private async void SetupBackfillTickets()
    {
#if DEDICATED_SERVER
        Debug.Log("SetupBackfillTickets");
        PayloadAllocation payloadAllocation = await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<PayloadAllocation>();

        backfillTicketId = payloadAllocation.BackfillTicketId;
        Debug.Log("backfillTicketId: " + backfillTicketId);

        acceptBackfillTicketsTimer = acceptBackfillTicketsTimerMax;
#endif
    }
    private async void HandleBackfillTickets()
    {
#if DEDICATED_SERVER
        if (NetworkManager.Singleton.ConnectedClients.Count < 2)
        {
            BackfillTicket backfillTicket = await MatchmakerService.Instance.ApproveBackfillTicketAsync(backfillTicketId);
            backfillTicketId = backfillTicketId;
        }
#endif
    }

    private void ChangeNetworkScene(ulong clientID)
    {
        if (NetworkManager.Singleton.ConnectedClients.Count == 2)
        {
            SceneEventProgressStatus eventStatus = NetworkManager.Singleton.SceneManager.LoadScene("Battle", LoadSceneMode.Single);
            Debug.Log("Event Status : " + eventStatus.ToString());
        }
    }

#if DEDICATED_SERVER
    private void MultiplayEventCallbacks_SubscriptionStateChanged(MultiplayServerSubscriptionState obj)
    {
        Debug.Log("DEDICATED_SERVER MultiplayEventCallbacks_SubscriptionStateChanged");
        Debug.Log(obj);
    }

    private void MultiplayEventCallbacks_Error(MultiplayError obj)
    {
        Debug.Log("DEDICATED_SERVER MultiplayEventCallbacks_Error");
        Debug.Log(obj.Reason);
    }

    private void MultiplayEventCallbacks_Deallocate(MultiplayDeallocation obj)
    {
        Debug.Log("DEDICATED_SERVER MultiplayEventCallbacks_Deallocate");
    }

    private void MultiplayEventCallbacks_Allocate(MultiplayAllocation obj)
    {
        Debug.Log("DEDICATED_SERVER MultiplayEventCallbacks_Allocate");

        if (alreadyAutoAllocated)
        {
            Debug.Log("Already auto allocated!");
            return;
        }

        alreadyAutoAllocated = true;

        var serverConfig = MultiplayService.Instance.ServerConfig;
        Debug.Log($"Server ID[{serverConfig.ServerId}]");
        Debug.Log($"AllocationID[{serverConfig.AllocationId}]");
        Debug.Log($"Port[{serverConfig.Port}]");
        Debug.Log($"QueryPort[{serverConfig.QueryPort}]");
        Debug.Log($"LogDirectory[{serverConfig.ServerLogDirectory}]");

        string ipv4Address = "0.0.0.0";
        ushort port = serverConfig.Port;
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipv4Address, port, "0.0.0.0");

        Debug.Log("Starting as Server!");
        NetworkManager.Singleton.StartServer();
        
    }
#endif

    [Serializable]
    public class PayloadAllocation
    {
        public Unity.Services.Matchmaker.Models.MatchProperties MatchProperties;
        public string GeneratorName;
        public string QueueName;
        public string PoolName;
        public string EnvironmentId;
        public string BackfillTicketId;
        public string MatchId;
        public string PoolId;
    }

}
