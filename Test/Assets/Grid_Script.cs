using UnityEngine;
using System;
using UnityEngine.UIElements;
using Unity.VisualScripting;


public class Grid_Script : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public class Building
    {
        public GameObject CurrentItem;
        public bool IsActive;
        public char Rotation;

    }

    public class Conveyor : Building
    {
        public float ConveyorSpeed;


        public Conveyor()
        {
            ConveyorSpeed = 0;
            CurrentItem = null;
            IsActive = true;
            Rotation = 'N';
            
        }

        public Conveyor(GameObject conveyorObject)
        {
            ConveyorSpeed = 1.0f;
            CurrentItem = null;
            IsActive = true;
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
        }
    }

    public static int GridSizeX = 26;
    public static int GridSizeY = 15;
    public const float X_WIDTH = 0.64f;
    public const float Y_WIDTH = 0.64f;
    public GameObject[,] Buildings = new GameObject[GridSizeX, GridSizeY];

    public GameObject conveyorPrefab; // Das Prefab des Conveyors
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
                conveyorChosen = false;
                Debug.Log($"Conveyor placed. Grid Coords: {(placePosition.x - 0.32f) / X_WIDTH}, {(placePosition.y - 0.32f) / Y_WIDTH}");

                Buildings[xGrid, yGrid] = currentConveyor;
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
    void Update()
    {
        if (isPlacingConveyor)
        {
            PlacingConveyor();
        }


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
