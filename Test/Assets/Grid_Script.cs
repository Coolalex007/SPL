using UnityEngine;
using System;

public class Grid_Script : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public class Conveyor
    {
        public GameObject CurrentItem;
        public bool IsActive;
        public char Rotation;

        public Conveyor()
        {
            CurrentItem = null;
            IsActive = true;
            Rotation = 'N';
            
        }
    }

    public static int GridSizeX = 16;
    public static int GridSizeY = 9;
    public const float X_WIDTH = 64f;
    public const float Y_WIDTH = 64f;
    public GameObject[,] Buildings = new GameObject[GridSizeX, GridSizeY];

    public GameObject conveyorPrefab; // Das Prefab des Conveyors
    public float rotationSpeed = 10f; // Geschwindigkeit, mit der der Conveyor gedreht wird
    private GameObject currentConveyor; // Der aktuell platzierte Conveyor

    void Start()
    {
        
    }

    void RotateConveyor()
    {
        transform.Rotate(Vector3.right);
    }

    void Update()
    {

        //Debug.Log("Joe ka wos do passiert");
        // Platzierungspunkt wird auf der getroffenen Oberfl‰che gesetzt
        Quaternion placeRotation = Quaternion.identity; // Keine Rotation, standardm‰ﬂig

        // Wenn mit der linken Maustaste geklickt wird, platziere den Conveyor
        if (Input.GetMouseButtonDown(0)) // Linke Maustaste
        {
            Vector3 placePosition = new Vector3();
            Debug.Log("Mousebutton down!!!");
            Vector3 vector = Input.mousePosition;

            Debug.Log(vector);
            float xPos = vector.x;
            float yPos = vector.y;

            int gridPosX = Convert.ToInt32(Mathf.Floor(xPos / X_WIDTH));
            int gridPosY = Convert.ToInt32(Mathf.Floor(yPos / Y_WIDTH));
            Debug.Log(Mathf.Floor(xPos / X_WIDTH));
            Debug.Log(gridPosX);

            placePosition.x = gridPosX * X_WIDTH;
            placePosition.y = gridPosY * Y_WIDTH;
            currentConveyor = Instantiate(conveyorPrefab, placePosition, placeRotation);
                
        }

        // Mit der rechten Maustaste kann der Conveyor rotiert werden
        if (Input.GetMouseButton(1)) // Rechte Maustaste
        {
            RotateConveyor();
        }
        
    }

}
