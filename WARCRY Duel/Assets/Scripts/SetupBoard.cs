using System.Collections.Generic;
using Unity.Netcode;

using UnityEngine;
using UnityEngine.SceneManagement;
// Tyler Arroyo
// Setup Board
// Sets the board up for the game.
public class SetupBoard : NetworkBehaviour
{
    // Delegates
    public delegate void CreateBoardDelegateHandler();

    // Events
    public static event CreateBoardDelegateHandler CreateBoard;

    // Fields
    [SerializeField] private List<Vector3> boardSpacesList;
    [SerializeField] private List<GameObject> selectedBoardSpace;
    [SerializeField] private GameObject[] boardSpacesArray;
    [SerializeField] private float spaceOffset;
    [SerializeField] public GameObject linePrefab;
    public Dictionary<GameObject, GameObject> boardSpacePairs = new Dictionary<GameObject, GameObject>() {};
    public Dictionary<GameObject, List<GameObject>> availablePaths = new Dictionary<GameObject, List<GameObject>>() { };

    public static SetupBoard Instance = null;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Setup Board Initiated!");
        NetworkManager.SceneManager.OnLoadEventCompleted += CreateLinePaths;
        boardSpacesArray = GameObject.FindGameObjectsWithTag("BoardSpace");
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void CreateLinePaths(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        foreach (GameObject boardSpace in boardSpacesArray)
        {
            // Doesn't draw a line if the board space is a Bench Slot or Infirmary
            if (boardSpace == null)
            {
                continue;
            }

            if(boardSpace.name.Contains("Bench") || boardSpace.name.Contains("Infirmary")) { continue; }

            // Attempts to grab the tile component from the Board Space
            Tile tile = null;
            try
            {
                tile = boardSpace.GetComponent<Tile>();
            }
            catch (System.Exception)
            {
                Debug.Log($"{boardSpace} had an error drawing a line.");
                continue;
            }

            if (tile != null)
            {
                foreach (Tile accessibleTile in tile.AccessibleTiles)
                {
                    GameObject lineObject = Instantiate(linePrefab, gameObject.transform);
                    LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        Debug.Log("Drawing Line!");
                        Vector3 initialPos;
                        Vector3 finalPos;
                        initialPos = boardSpace.transform.position;
                        finalPos = accessibleTile.transform.position;
                        finalPos.y = -0.96f - 0.0002f;
                        initialPos.y = -0.96f - 0.0001f;
                        initialPos = Vector3.MoveTowards(initialPos, finalPos, spaceOffset);
                        finalPos = Vector3.MoveTowards(finalPos, initialPos, spaceOffset);
                        lineRenderer.SetPosition(0, initialPos);
                        lineRenderer.SetPosition(1, finalPos);
                        lineRenderer.startWidth = 0.003f;
                        lineRenderer.endWidth = 0.003f;
                    }
                }
            }
        }
        CreateBoard?.Invoke();
    }
    public void FindAvailablePaths()
    {
        availablePaths.Clear();
        foreach (KeyValuePair<GameObject,GameObject> item in boardSpacePairs)
        {
            // For every position not currently in the list, creates a new dictionary key with an empty list to use later

            if (availablePaths.ContainsKey(item.Key) == false)
            {
                availablePaths.Add(item.Key, new List<GameObject> { });
            }
            
            if (availablePaths.ContainsKey(item.Value) == false)
            {
                availablePaths.Add(item.Value, new List<GameObject> { });
            }

        }
        Dictionary<GameObject, List<GameObject>> tempDict = availablePaths;

        foreach (KeyValuePair<GameObject,GameObject> item in boardSpacePairs)
        {
            if (availablePaths.ContainsKey(item.Key))
            {
                if (item.Value.tag == "BoardSpaceChild")
                {
                    tempDict[item.Key].Add(item.Value.transform.parent.gameObject);
                }
                else
                {
                    tempDict[item.Key].Add(item.Value);
                }
                
            }

            if (availablePaths.ContainsKey(item.Value))
            {
                if (item.Key.tag == "BoardSpaceChild")
                {
                    tempDict[item.Value].Add(item.Key.transform.parent.gameObject);
                }
                else
                {
                    tempDict[item.Value].Add(item.Key);
                }
            }
        }
        availablePaths = tempDict;

        // Checks for all child board spaces and appends the link to the parent Board Space
        List<KeyValuePair<GameObject, GameObject>> boardSpaceArray = new List<KeyValuePair<GameObject, GameObject>> { };
        List<GameObject> childObjectArray = new List<GameObject> { };
        foreach (KeyValuePair<GameObject, List<GameObject>> boardSpace in availablePaths)
        {
            if (boardSpace.Key.tag == "BoardSpace")
            {
                foreach (KeyValuePair<GameObject, List<GameObject>> child in availablePaths)
                {
                    if (child.Key.tag == "BoardSpaceChild")
                    {
                        if (child.Key.name.Contains(boardSpace.Key.name))
                        {
                            boardSpaceArray.Add(new KeyValuePair<GameObject, GameObject> (boardSpace.Key, child.Value[0]));
                            childObjectArray.Add(child.Key);
                        }
                    }

                }
            }
        }
        foreach (KeyValuePair<GameObject,GameObject> boardSpaceKey in boardSpaceArray)
        {
            availablePaths[boardSpaceKey.Key].Add(boardSpaceKey.Value);
        }
        foreach (GameObject childObjectKey in childObjectArray)
        {
            availablePaths.Remove(childObjectKey);
        }
    }
}
