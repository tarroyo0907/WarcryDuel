using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Multiplayer_Network;

#if DEDICATED_SERVER
using Unity.Services.Multiplay;
#endif

// Tyler Arroyo
// Main Menu Manager
// Manages the buttons in the Main Menu
public class MainMenuManager : NetworkBehaviour
{
    // Fields
    #region References
    [Header("Canvas References")]
    [SerializeField] private Button battleButton;
    [SerializeField] private Button partyButton;
    [SerializeField] private Button startServerButton;

    [SerializeField] private GameObject partyFigurines;
    [SerializeField] private GameObject favoriteCharacter;
    [SerializeField] private GameObject[] partyFiguresArray;

    [SerializeField] private GameObject homeCanvasView;
    [SerializeField] private GameObject partyCanvasView;
    [SerializeField] private GameObject partyFormationView;

    [SerializeField] private GameObject backButton;
    [SerializeField] private GameObject homeMenuButtons;

    [SerializeField] private GameObject partyNamePanel;
    [SerializeField] private GameObject characterCenterPosition;

    [SerializeField] private GameObject partyFormationButton;
    [SerializeField] private GameObject partyFormationPosition;
    [SerializeField] private GameObject partyFormationBackground;
    [SerializeField] private GameObject collectionFigures;

    [SerializeField] private GameObject collectionPosition;
    #endregion

    #region Ticket Data
    [Space(10)]
    [Header("Ticket Data")]
    private bool showingHomeMenuButtons = true;

    private CreateTicketResponse createTicketResponse;
    [SerializeField] private float pollTicketTimerMax = 1.1f;
    [SerializeField] private float pollTicketTimer;
    #endregion

    #region State Info
    private enum MenuStates { Home, PartyOverview, CollectionView, Shop, Campaign, Event }
    [SerializeField] private MenuStates currentMenuState = MenuStates.Home;

    private bool spawnedSavedParty = false;
    private Vector3 partyFigurinesOriginalPos;
    private bool loadingStarted = false;
    [SerializeField] private bool localTesting = false;
    #endregion

    #region Collection Data
    public string[] _figureNames = new string[] { };

    private GameObject selectedCollectionFigurine;
    #endregion

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

        InitializePartyData();

#if DEDICATED_SERVER
        await UnityServices.InitializeAsync();

        backfillTicketId = null;
        serverQueryHandler = null;

        // Handles Backfill Tickets
        //SetupBackfillTickets();

        var serviceConfig = MultiplayService.Instance.ServerConfig;
        ushort port = serviceConfig.Port;
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("0.0.0.0", port);

        Debug.Log("Starting as Server!");
        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.StartServer();
        }
        else
        {
            Debug.Log("Server already running, skipping StartServer().");
        }



        // Read ports & paths that Multiplay injected
        Debug.Log($"ServerId:{serviceConfig.ServerId} AllocId:{serviceConfig.AllocationId} Port:{serviceConfig.Port} Query:{serviceConfig.QueryPort} Logs:{serviceConfig.ServerLogDirectory}");

        // Start SQP and keep it updated
        serverQueryHandler = await MultiplayService.Instance.StartServerQueryHandlerAsync(
            maxPlayers: 2, serverName: "RankedMatch", gameType: "Ranked",
            buildId: Application.version, map: "Battle");

        serverQueryHandler.CurrentPlayers = 0;
        serverQueryHandler.Port = port;            // expose the same port clients use
        serverQueryHandler.MaxPlayers = 2;
        serverQueryHandler.UpdateServerCheck();

        // Only after allocation completes and your game is ready:
        await MultiplayService.Instance.ReadyServerForPlayersAsync();
#endif
    }

    // Start is called before the first frame update
    void Start()
    {
        #region Sets Screen Settings
        if (!IsServer)
        {
            //Screen.SetResolution(450, 950, false);
            Screen.fullScreenMode = FullScreenMode.Windowed;
        }
        #endregion

        battleButton.onClick.AddListener(() =>
        {
            if (localTesting)
            {
                FindLocalMatch();
            }
            else
            {
                FindMatch();
            }
            
        });

        startServerButton.onClick.AddListener(() => { StartLocalServer(); });
    }

    // Update is called once per frame
    async void Update()
    {
        #region Screen Settings
        if (Screen.width > (Screen.height / 19) * 9)
        {
            Screen.SetResolution((Screen.height / 19) * 9, Screen.height, FullScreenMode.Windowed);
        }
        #endregion

        #region Ticket Management
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
        #endregion

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

        #region Player Input
        // Checks if the player clicks/taps on an object
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            try
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                // Casts the ray and get the first game object hit
                Physics.Raycast(ray, out hit);

                if (hit.collider != null)
                {
                    if (hit.collider.tag == "Figurine")
                    {
                        ClickFigurine(hit);
                        Debug.Log("Clicked Figurine");
                    }

                }
            }
            catch (Exception)
            {
            }
            
        }
        #endregion
    }


    private void InitializePartyData()
    {
        string savedTeamPath = Application.persistentDataPath + "/savedTeam.txt";
        string playerCollectionPath = Application.persistentDataPath + "/playerCollection.txt";
        if (File.Exists(savedTeamPath) && File.Exists(playerCollectionPath))
        {
            return;
        }
        else
        {
            Debug.Log("Initializing Party Data");
            // Set Party Data
            CollectionObject partyTeam = new CollectionObject
            {
                figureNames = new string[6] { "Bulwark_Figurine", "Bastion_Figurine", "Shade_Figurine", "Shade_Figurine", "Rook_Figurine", "Rook_Figurine" }
            };

            // Convert Collection Object to JSON format string
            string json = JsonUtility.ToJson(partyTeam); 

            // Write the Json String to Text File
            File.WriteAllText(Application.persistentDataPath + "/savedTeam.txt", json);

            // Set Collection Data
            CollectionObject collectionObject = new CollectionObject
            {
                figureNames = new string[12] { "Bulwark_Figurine", "Bastion_Figurine", "Shade_Figurine", "Shade_Figurine", "Rook_Figurine", "Rook_Figurine", "Bulwark_Figurine", "Bastion_Figurine", "Shade_Figurine", "Shade_Figurine", "Rook_Figurine", "Rook_Figurine" }
            };

            // Convert Collection Object to JSON format string
            json = JsonUtility.ToJson(collectionObject);

            // Write the Json String to Text File
            File.WriteAllText(Application.persistentDataPath + "/playerCollection.txt", json);
        }
    }

    private void ClickFigurine(RaycastHit hit)
    {
        // Complete different logic based on the menu state
        switch (currentMenuState)
        {
            case MenuStates.Home:
                break;

            case MenuStates.PartyOverview:
                break;

            // If the Menu State is in Collection View
            case MenuStates.CollectionView:
                // Check if the figure is a party figurine or a collection figurine
                GameObject clickedFigurine = hit.collider.gameObject;
                if (clickedFigurine.transform.parent.name == "Party Figurines")
                {
                    // Figurine is a Party Figurine
                    if (selectedCollectionFigurine == null)
                    {
                        return;
                    }
                    else
                    {
                        // Switch Party Figurine with Collection Figurine
                        Vector3 collectionFigurinePos = selectedCollectionFigurine.transform.position;
                        selectedCollectionFigurine.transform.position = clickedFigurine.transform.position;
                        selectedCollectionFigurine.transform.SetParent(clickedFigurine.transform.parent);

                        clickedFigurine.transform.SetParent(collectionFigures.transform);
                        clickedFigurine.transform.position = collectionFigurinePos;
                        

                        Destroy(selectedCollectionFigurine.transform.Find("SelectedIcon(Clone)").gameObject);
                        selectedCollectionFigurine = null;
                        return;
                    }

                }
                else if (clickedFigurine.transform.parent.name == "Collection Figures")
                {
                    // Figurine is a Collection Figurine
                    Debug.Log("Selected Collection Figurine");

                    // Removes the Selected Icon from the previously selected figurine
                    if (selectedCollectionFigurine != null)
                    {
                        Destroy(selectedCollectionFigurine.transform.Find("SelectedIcon(Clone)").gameObject);
                    }

                    // Add a Selected Icon to the new clicked Figurine
                    selectedCollectionFigurine = hit.collider.gameObject;

                    GameObject selectedIcon = Resources.Load<GameObject>("UI_Icons/SelectedIcon");

                    Instantiate(selectedIcon, selectedCollectionFigurine.transform);
                }
                

                break;

            case MenuStates.Shop:
                break;

            case MenuStates.Campaign:
                break;

            case MenuStates.Event:
                break;

            default:
                break;
        }
    }

    public void OpenPartyMenu()
    {
        // Hides Home View Objects
        homeCanvasView.SetActive(false);

        // Shows Party View Objects
        partyFigurines.SetActive(true);
        partyCanvasView.SetActive(true);
        backButton.SetActive(true);

        if (spawnedSavedParty == false)
        {
            // Grabs all Party Figurine Spaces
            GameObject[] partyFigurinePositions = new GameObject[6];
            for (int i = 0; i < 6; i++)
            {
                partyFigurinePositions[i] = partyFigurines.transform.GetChild(i).gameObject;
            }

            // Fill Party Figurines Spaces with the player's figurines
            // Starts by reading from the JSON file that stores the party information
            string[] savedPartyFigurines = LoadParty();

            // Spawns each figurine into it's space.
            for (int i = 0; i < savedPartyFigurines.Length; i++)
            {
                // Grab the Figurine To Spawn
                GameObject figurineToSpawn = (GameObject)Resources.Load($"Figurines/{savedPartyFigurines[i]}");

                // Adjust Position based on Index
                Vector3 spawnPosition = partyFigurinePositions[i].transform.position;

                // Spawn Figurine at new Position
                GameObject spawnedFigurine = Instantiate(figurineToSpawn, spawnPosition, partyFigurinePositions[i].transform.rotation, partyFigurines.transform);
                spawnedFigurine.transform.localScale = partyFigurinePositions[i].transform.localScale;
                Destroy(spawnedFigurine.GetComponent<NetworkObject>());
                Destroy(spawnedFigurine.GetComponent<Figurine>());
                Destroy(partyFigurinePositions[i]);

                spawnedFigurine.name = savedPartyFigurines[i];
            }

            spawnedSavedParty = true;
            partyFigurinesOriginalPos = partyFigurines.transform.position;
        }
        
    }

    public void SelectMenuButton(Button clickedButton)
    {
        // Switches between showing the home button and menu buttons
        if (homeCanvasView.activeSelf)
        {
            // Hide Home Menu Buttons
            homeMenuButtons.SetActive(false);

            // Show Home Button
            backButton.SetActive(true);
        }

        // Sets the current menu state based on the button clicked
        Debug.Log("Clicked Button : " + clickedButton.name);
        switch (clickedButton.name)
        {
            case "Home Button":
                currentMenuState = MenuStates.Home;
                break;

            case "Party Button":
                currentMenuState = MenuStates.PartyOverview;
                break;

            case "Party Formation Button":
                currentMenuState = MenuStates.CollectionView;
                break;
            default:
                break;
        }
    }

    public void OpenHomeMenu()
    {
        // Hides Party View Objects
        partyFigurines.SetActive(false);
        partyCanvasView.SetActive(false);

        // Shows Party View Objects
        favoriteCharacter.SetActive(true);
        homeCanvasView.SetActive(true);
    }

    public void OpenPartyFormationMenu()
    {
        // Hide Party View and Party Formation Button
        partyFigurines.transform.SetParent(partyFormationBackground.transform);
        partyCanvasView.SetActive(false);

        // Play Animation to move party figurines up
        partyFigurines.transform.position = partyFormationPosition.transform.position;

        // Show Party Formation View
        partyFormationView.SetActive(true);

        // Spawn Player Collection
        SpawnCollection();
    }

    IEnumerator MoveObjectAnimation(GameObject objectToMove, GameObject objectToMoveTo, bool scaling)
    {
        // Interpolates the position between the 2 objects
        float timerCount = 0;
        Vector3 startingPos = objectToMove.transform.position;
        Vector3 endingPos = objectToMoveTo.transform.position;
        while (timerCount < 1)
        {
            yield return new WaitForEndOfFrame();
            timerCount += 1.5f * Time.deltaTime;
            objectToMove.transform.position = Vector3.Lerp(startingPos, endingPos, timerCount);

            if (scaling)
            {
                objectToMove.transform.localScale = Vector3.Lerp(objectToMove.transform.localScale, new Vector3(3, 3, 3), timerCount);
            }

        }
    }

    public void SpawnCollection()
    {
        // Loads Player Collection from JSON File
        LoadCollection();

        // Spawns each figurine
        
        for (int i = 0; i < _figureNames.Length; i++)
        {
            // Grab the Figurine To Spawn
            GameObject figurineToSpawn = (GameObject) Resources.Load($"Figurines/{_figureNames[i]}");

            // Adjust Position based on Index
            int rowAmount = i % 3;
            int columnAmount = i / 3;
            Vector3 SpawnPosition = collectionPosition.transform.position + new Vector3(10 * columnAmount, -14 * rowAmount, 0);

            // Spawn Figurine at new Position
            GameObject spawnedFigurine = Instantiate(figurineToSpawn, SpawnPosition, collectionPosition.transform.rotation, collectionFigures.transform);
            spawnedFigurine.transform.localScale = new Vector3(13.36219f, 13.36219f, 13.36219f);
            Destroy(spawnedFigurine.GetComponent<NetworkObject>());
            Destroy(spawnedFigurine.GetComponent<Figurine>());

            spawnedFigurine.name = _figureNames[i];
        }
    }

    public void SaveParty()
    {
        // Save the player's current party.
        SavePartyData();
        SaveCollection();

        // Go back to the Party Select Menu
        // Hide Collection Menu
        partyFormationView.SetActive(false);

        // Show Party View
        partyFigurines.SetActive(true);
        partyCanvasView.SetActive(true);
        partyFigurines.transform.SetParent(partyCanvasView.transform);
        partyFigurines.transform.position = partyFigurinesOriginalPos;

        // Change Menu State
        currentMenuState = MenuStates.PartyOverview;
    }

    public void OpensPreviousMenu()
    {
        switch (currentMenuState)
        {
            case MenuStates.PartyOverview:
                partyCanvasView.SetActive(false);
                backButton.SetActive(false);
                homeCanvasView.SetActive(true);
                homeMenuButtons.SetActive(true);
                currentMenuState = MenuStates.Home;
                break;

            case MenuStates.CollectionView:
                partyFigurines.transform.SetParent(partyCanvasView.transform);
                partyFigurines.transform.position = partyFigurinesOriginalPos;
                partyFormationView.SetActive(false);
                partyCanvasView.SetActive(true);
                currentMenuState = MenuStates.PartyOverview;
                break;

            case MenuStates.Shop:
                break;
            case MenuStates.Campaign:
                break;
            case MenuStates.Event:
                break;
            default:
                break;
        }
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

    #region Matchmaking Functionality
    private async void FindMatch()
    {
        Debug.Log("Finding Match...");
        SceneManager.LoadScene("Loading");

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
                    
                    GameObject sceneManager = GameObject.Find("SceneManager");
                    GameObject networkManager = GameObject.Find("NetworkManager");
                    GameObject soundManager = GameObject.Find("SoundManager");
                    Destroy(sceneManager);
                    Destroy(networkManager);
                    Destroy(soundManager);

                    SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                    break;

                case MultiplayAssignment.StatusOptions.Failed:
                    createTicketResponse = null;
                    Debug.Log("Failed to create Multiplay server!");

                    sceneManager = GameObject.Find("SceneManager");
                    networkManager = GameObject.Find("NetworkManager");
                    soundManager = GameObject.Find("SoundManager");

                    Destroy(sceneManager);
                    Destroy(networkManager);
                    Destroy(soundManager);

                    SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
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
        #if DEDICATED_SERVER
        if (serverQueryHandler != null)
        {
            serverQueryHandler.CurrentPlayers = (ushort) NetworkManager.Singleton.ConnectedClients.Count;
        }
        #endif

        if (NetworkManager.Singleton.ConnectedClients.Count == 2 && loadingStarted == false)
        {
            SceneEventProgressStatus eventStatus = NetworkManager.Singleton.SceneManager.LoadScene("Battle", LoadSceneMode.Single);
            Debug.Log("Event Status : " + eventStatus.ToString());
            loadingStarted = true;
        }
    }
#endregion

    #region Data Manipulation
    public class CollectionObject
    {
        public string[] figureNames;
    }

    public void SaveCollection()
    {
        // Sets Variable Values
        int childCount = collectionFigures.transform.childCount;
        _figureNames = new string[childCount];
        for (int i = 0; i < childCount; i++)
        {
            _figureNames[i] = collectionFigures.transform.GetChild(i).name;
            _figureNames[i].Replace("(Clone)", "");
        }

        // Puts Variables into Collection Object
        CollectionObject collectionObject = new CollectionObject
        {
            figureNames = _figureNames
        };

        // Convert Collection Object to JSON format string
        string json = JsonUtility.ToJson(collectionObject);

        // Write the Json String to Text File
        File.WriteAllText(Application.persistentDataPath + "/playerCollection.txt", json);
    }

    public void SavePartyData()
    {
        Debug.Log("Saving to " + Application.persistentDataPath);

        // Sets Variable Values
        int childCount = partyFigurines.transform.childCount;
        _figureNames = new string[childCount];
        for (int i = 0; i < childCount; i++)
        {
            string figureName = partyFigurines.transform.GetChild(i).name;
            figureName.Replace("(Clone)", "");
            _figureNames[i] = figureName;
        }

        // Puts Variables into Collection Object
        CollectionObject collectionObject = new CollectionObject
        {
            figureNames = _figureNames
        };

        // Convert Collection Object to JSON format string
        string json = JsonUtility.ToJson(collectionObject);

        // Write the Json String to Text File
        File.WriteAllText(Application.persistentDataPath + "/savedTeam.txt", json);
    }

    public void LoadCollection()
    {
        if (File.Exists(Application.persistentDataPath + "/playerCollection.txt"))
        {
            // Reads json text from the file into saveString
            string saveString = File.ReadAllText(Application.persistentDataPath + "/playerCollection.txt");
            Debug.Log("Loaded: " + saveString);

            // Converts JSON text into Collection Object
            CollectionObject collectionObject = JsonUtility.FromJson<CollectionObject>(saveString);

            _figureNames = collectionObject.figureNames;
        }
    }

    public string[] LoadParty()
    {
        if (File.Exists(Application.persistentDataPath + "/savedTeam.txt"))
        {
            // Reads json text from the file into saveString
            string saveString = File.ReadAllText(Application.persistentDataPath + "/savedTeam.txt");

            // Converts JSON text into Collection Object
            CollectionObject partyObject = JsonUtility.FromJson<CollectionObject>(saveString);

            return partyObject.figureNames;
        }

        return null;
    }



    #endregion

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
