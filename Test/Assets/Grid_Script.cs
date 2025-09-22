using UnityEngine;
using System;
using UnityEngine.UIElements;
using Unity.VisualScripting;


public class Grid_Script : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public class Item
    {
        public string Name;
        public double Value;
        public GameObject ItemObject;

        public Item(string name, GameObject gameObject)
        {
            Name = name;
            Value = 0;
            ItemObject = gameObject;
        }
    }


    public class Building
    {
        private Item _currentItem;
        public GameObject CurrentItemObject;
        public bool IsActive;
        public char Rotation;
        public int xPos, yPos;

        public Item CurrentItem
        {
            get => _currentItem;
            set
            {
                _currentItem = value;
                if (_currentItem is null)
                {
                    CurrentItem.ItemObject = null;
                }
                else
                {
                    
                    CurrentItemObject = CurrentItem.ItemObject;
                    
                }
            }
        }
    }

    public class Conveyor : Building
    {
        public float ConveyorSpeed;
        private float CurrentLerp;
        private int Endpoint = 1;
        private LineRenderer _lineRenderer;

        public Conveyor()
        {
            ConveyorSpeed = 0;
            CurrentItemObject = null;
            IsActive = true;
            Rotation = 'N';
            xPos = 0;
            yPos = 0;
            
        }

        public Conveyor(GameObject conveyorObject)
        {
            ConveyorSpeed = 1.0f;
            CurrentItemObject = null;
            IsActive = false;
            float zRotation = conveyorObject.transform.eulerAngles.z;
            switch (zRotation)
            {
                case 0:
                    Rotation = 'N';
                    break;
                case 90:
                    Rotation = 'E';
                    break;
                case 180:
                    Rotation = 'S';
                    break;
                case 270:
                    Rotation = 'W';
                    break;
            }
            xPos = Convert.ToInt32(Mathf.Floor(conveyorObject.transform.position.x / X_WIDTH));
            yPos = Convert.ToInt32(Mathf.Floor(conveyorObject.transform.position.y / Y_WIDTH));
        }

        public void Tick()
        {
            if (CurrentItemObject is not null)
            {
                Debug.Log("movingObject");
                CurrentItemObject.transform.position = Vector3.Lerp(_lineRenderer.GetPosition(Endpoint - 1), _lineRenderer.GetPosition(Endpoint), CurrentLerp);
                CurrentLerp += ConveyorSpeed * Time.deltaTime;

                if (CurrentLerp >= 1)
                {
                    if (FindNextBuilding(this, xPos, yPos))
                    {
                        CurrentLerp = 0;
                    }
                    else
                    {
                        CurrentLerp = 1;
                    }

                }
            }

        }

        

        
    }

    public static int GridSizeX = 26;
    public static int GridSizeY = 15;
    public const float X_WIDTH = 0.64f;
    public const float Y_WIDTH = 0.64f;
    public static Building[,] Buildings = new Building[GridSizeX, GridSizeY];

    public GameObject conveyorPrefab; // Das Prefab des Conveyors
    public GameObject orePrefab;
    public float rotationSpeed = 10f; // Geschwindigkeit, mit der der Conveyor gedreht wird
    private GameObject currentConveyor; // Der aktuell platzierte Conveyor

    void Start()
    {

    }

    void RotateConveyor()
    {
        Vector3 rotationRight = new Vector3(0, 0, -90);
        currentConveyor.transform.eulerAngles += rotationRight;
    }

    Vector3 FindMouseGridCoords()
    {
        Vector3 placePosition = new Vector3();
        Vector3 vector = Camera.main.ScreenToWorldPoint(Input.mousePosition);


        float xPos = vector.x;
        float yPos = vector.y;

        int gridPosX = Convert.ToInt32(Mathf.Floor(xPos / X_WIDTH));
        int gridPosY = Convert.ToInt32(Mathf.Floor(yPos / Y_WIDTH));

        placePosition.x = gridPosX * 0.64f + 0.32f;
        placePosition.y = gridPosY * 0.64f + 0.32f;

        return placePosition;
    }

    bool conveyorChosen = false;
    bool locked = false;
    void PlacingConveyor()
    {
        Quaternion placeRotation = Quaternion.identity; // Keine Rotation, standardm‰ﬂig
        Vector3 placePosition = FindMouseGridCoords();
        bool mouseLeftPressed = Input.GetMouseButtonDown(0);
        bool mouseLeftReleased = Input.GetMouseButtonUp(0);

        if (mouseLeftPressed && conveyorChosen == true)
        {
            int xGrid = Convert.ToInt32((placePosition.x - 0.32f) / X_WIDTH);
            int yGrid = Convert.ToInt32((placePosition.y - 0.32f) / Y_WIDTH);

            if (Buildings[xGrid, yGrid] == null)
            {
                Conveyor conveyor = new Conveyor(currentConveyor);
                conveyorChosen = false;
                conveyor.IsActive = true;
                Debug.Log($"Conveyor placed. Grid Coords: {(placePosition.x - 0.32f) / X_WIDTH}, {(placePosition.y - 0.32f) / Y_WIDTH}");

                Buildings[xGrid, yGrid] = conveyor;
                isPlacingConveyor = false;
                currentConveyor = null;
            }

        }
        else if (conveyorChosen)
        {

            currentConveyor.transform.position = placePosition;
        }
        else if (mouseLeftPressed && conveyorChosen == false) // Linke Maustaste
        {
            int xGrid = Convert.ToInt32((placePosition.x - 0.32f) / X_WIDTH);
            int yGrid = Convert.ToInt32((placePosition.y - 0.32f) / Y_WIDTH);
            if (Buildings[xGrid, yGrid] is null)
            {
                currentConveyor = Instantiate(conveyorPrefab, placePosition, placeRotation);
                currentConveyor.transform.parent = this.transform;
                conveyorChosen = true;
            }

        }


        // Mit der rechten Maustaste kann der Conveyor rotiert werden
        if (Input.GetKeyDown(KeyCode.R)) // Rechte Maustaste
        {
            if (!locked)
            {
                locked = true;
                RotateConveyor();
            }

        }
        else
        {
            locked = false;
        }

    }

    bool isPlacingConveyor = false;
    bool isDebugItemPlacing = false;
    void Update()
    {
        if (isPlacingConveyor)
        {
            PlacingConveyor();
        }
        if (isDebugItemPlacing)
        {
            PlacingItem();
        }

        for(int i = 0; i < GridSizeX; i++)
        {
            for (int j = 0; j < GridSizeY; j++)
            {
                if (Buildings[i,j] is Conveyor)
                {
                    Conveyor conveyor = Buildings[i,j] as Conveyor;
                    conveyor.Tick();
                }
            }
        }


    }

    bool ItemChosen = false;
    Item CurrentItem;
    void PlacingItem()
    {
        Vector3 placePosition = FindMouseGridCoords();
        if (!ItemChosen)
        {
            GameObject newItem = Instantiate(orePrefab, FindMouseGridCoords(), Quaternion.identity);
            CurrentItem = new Item("NewTestOre", newItem);
            ItemChosen = true;
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                
                int xGrid = Convert.ToInt32((placePosition.x - 0.32f) / X_WIDTH);
                int yGrid = Convert.ToInt32((placePosition.y - 0.32f) / Y_WIDTH);
                
                if (Buildings[xGrid, yGrid] is Conveyor)
                {
                    Buildings[xGrid, yGrid].CurrentItem = CurrentItem;
                    ItemChosen = false;
                    CurrentItem = null;
                    isDebugItemPlacing = false;
                }
            }
            else
            {
                CurrentItem.ItemObject.transform.position = placePosition;
            }
        }


        
    }

    public static bool FindNextBuilding(Conveyor conveyor, int xPos, int yPos)
    {
        switch (conveyor.Rotation)
        {
            case 'N':
                yPos++;
                break;
            case 'E':
                xPos++;
                break;
            case 'S':
                yPos--;
                break;
            case 'W':
                xPos--;
                break;
        }

        Building nextBuilding = Buildings[xPos, yPos];
        if (nextBuilding is not null)
        {
            if (nextBuilding.CurrentItem is null)
            {
                nextBuilding.CurrentItem = conveyor.CurrentItem;
                conveyor.CurrentItem = null;
                return true;
            }

        }
        return false;
    }

    bool lockedGui = false;
    bool toggleGui = false;
    private void OnGUI()
    {
        if (toggleGui)
        {
            GUI.Box(new Rect(10, 10, 300, 90), "joe");
            if (GUI.Button(new Rect(20, 40, 80, 20), "Conveyor"))
            {
                isPlacingConveyor ^= true;
                isDebugItemPlacing = false;
            }
            if (GUI.Button(new Rect(100, 40, 80, 20), "OrePlace"))
            {
                isDebugItemPlacing ^= true;
                isPlacingConveyor = false;
            }
        }

        
        if (Input.GetKeyDown(KeyCode.B))
        {
            Debug.Log("B pressed");
            if (lockedGui == false)
            {
                Debug.Log(GUI.enabled);
                lockedGui = true;
                toggleGui ^= true;
            }

        }
        else
        {
            lockedGui = false;
        }
    }
}
