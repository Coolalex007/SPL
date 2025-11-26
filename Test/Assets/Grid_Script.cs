using UnityEngine;
using System;
using System.Collections.Generic;

public class Grid_Script : MonoBehaviour
{
    public static Grid_Script Instance { get; private set; }

    // ==== Grid ====
    public static int GridSizeX = 26;
    public static int GridSizeY = 15;
    public const float CELL_W = 0.64f;
    public const float CELL_H = 0.64f;
    public const float ITEM_Z = -0.1f;

    public static Building[,] Buildings = new Building[GridSizeX, GridSizeY];
    ResourceNode[,] resourceNodes = new ResourceNode[GridSizeX, GridSizeY];

    Dictionary<string, MaterialDef> materials = new Dictionary<string, MaterialDef>();
    List<MaterialDef> materialList = new List<MaterialDef>();

    // ==== Prefabs ====
    public GameObject conveyorPrefab;
    public GameObject orePrefab;
    public GameObject ingotPrefab;
    public GameObject furnacePrefab;
    public GameObject splitter3Prefab;   // 3-Way-Splitter
    public GameObject minerPrefab;
    public GameObject resourceNodePrefab;

    // ==== Furnace Sprites ====
    public Sprite furnaceOffSprite;
    public Sprite furnaceOnSprite;

    // ==== Platzierung ====
    GameObject ghost;
    bool placingConveyor;
    bool placingFurnace;
    bool placingSplitter;
    bool placingMiner;

    // ==== Debug-Item auf Conveyor setzen ====
    bool placingItemOnConveyor;
    GameObject itemGhost;

    // ==== Auswahl / Bearbeitung ====
    Building selected;
    bool dragging;

    double currentInventoryValue;

    // ==== Datentypen ====
    public enum Dir { N, E, S, W }

    public enum ItemType { Ore, Ingot }

    public class MaterialDef
    {
        public string id;
        public Color color;
        public double baseValue;

        public MaterialDef(string id, Color color, double baseValue)
        {
            this.id = id;
            this.color = color;
            this.baseValue = baseValue;
        }
    }

    public class Item
    {
        public string id;
        public GameObject go;
        public double value;
        public Item(string id, GameObject go) 
        { 
            this.id = id; 
            this.go = go; 
        }
        public MaterialDef material;
        public ItemType type;
        public TextMesh valueLabel;
    }

    public class ResourceNode
    {
        public int x, y;
        public MaterialDef material;
        public GameObject go;
    }

    public class Building
    {
        public Item item;
        public Dir rot;
        public int x, y;
        public GameObject go;

        public virtual bool CanAccept(Item i) 
        { 
            return item == null; 
        }
        public virtual bool Accept(Item i) 
        { 
            if (!CanAccept(i)) return false; item = i; return true; 
        }
        public virtual void Tick(float dt) { }
        public virtual void OnMoved() 
        {
            if (go != null) go.transform.position = Center(); 
        }
        public virtual bool IsRotatable()
        { 
            return false; 
@@ -205,63 +239,93 @@ public class Grid_Script : MonoBehaviour
            if (sr == null) return;
            sr.sprite = (item != null) ? onSprite : offSprite;
        }

        public override bool Accept(Item i)
        {
            if (!base.Accept(i)) return false;
            t = 0f;
            UpdateSprite();
            if (i.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                i.go.transform.position = p;
                var sr = i.go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;
            }
            return true;
        }

        public override void Tick(float dt)
        {
            if (item == null) return;
            t += dt;
            if (t >= processTime)
            {
                item.value *= 2;
                double newValue = item.value + 1;
                var mat = item.material;
                if (item.go != null)
                {
                    Destroy(item.go);
                }

                item = null;
                item = Grid_Script.Instance.CreateItem(mat, ItemType.Ingot, Center(), newValue);
                UpdateValueLabel(item);

                bool output = TryOutput();
                if (output)
                {
                    item = null;
                }
                else if (item != null && item.go != null)
                {
                    Vector3 p = Center(); p.z = ITEM_Z;
                    item.go.transform.position = p;
                }

                t = 0f;
                UpdateSprite();
                

            }
        }

        bool TryOutput()
        {
            var step = StepForDir(rot);
            int tx = x + step.dx;
            int ty = y + step.dy;

            if (!InBounds(tx, ty)) return false;

            var nb = Buildings[tx, ty];
            if (nb == null) return false;
            if (!nb.CanAccept(item)) return false;

            bool ok = nb.Accept(item);
            return ok;
        }

        public override void OnMoved()
        {
            base.OnMoved();
            UpdateSprite();
        }
    }

    public class Splitter3 : Building
    {
        int rrIndex; // 0=Left, 1=Forward, 2=Right

        public override bool Accept(Item i)
        {
            if (!base.Accept(i)) return false;
            if (i.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                i.go.transform.position = p;
                var sr = i.go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;
            }
            return true;
        }

        public override void Tick(float dt)
@@ -309,123 +373,196 @@ public class Grid_Script : MonoBehaviour
                case Dir.E: rot = Dir.S; break;
                case Dir.S: rot = Dir.W; break;
                case Dir.W: rot = Dir.N; break;
            }
            if (go != null) go.transform.rotation = Quaternion.Euler(0, 0, DirToAngle(rot));
        }

        public override void OnMoved()
        {
            base.OnMoved();
            if (item != null && item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
        }

        Dir DirForIndex(int idx)
        {
            if (idx == 0) return LeftOf(rot);
            if (idx == 1) return rot;          // forward
            return RightOf(rot);
        }
    }

    public class Miner : Building
    {
        public ResourceNode node;
        public float mineInterval = 2.5f;
        float t;
        public Grid_Script owner;

        public override bool IsRotatable() { return true; }

        public override void RotateClockwise()
        {
            switch (rot)
            {
                case Dir.N: rot = Dir.E; break;
                case Dir.E: rot = Dir.S; break;
                case Dir.S: rot = Dir.W; break;
                case Dir.W: rot = Dir.N; break;
            }
            if (go != null) go.transform.rotation = Quaternion.Euler(0, 0, DirToAngle(rot));
        }

        public override bool CanAccept(Item i)
        {
            return false;
        }

        public override void Tick(float dt)
        {
            if (node == null || owner == null) return;

            t += dt;
            if (t < mineInterval) return;

            var step = StepForDir(rot);
            int tx = x + step.dx;
            int ty = y + step.dy;

            if (!InBounds(tx, ty)) return;

            var nb = Buildings[tx, ty];
            if (nb == null) return;
            if (!nb.CanAccept(null)) return;

            Item ore = owner.CreateItem(node.material, ItemType.Ore, owner.CellCenter(x, y), node.material.baseValue);
            owner.UpdateValueLabel(ore);

            if (nb.Accept(ore))
            {
                t = 0f;
            }
            else
            {
                owner.DestroyItemVisual(ore);
            }
        }
    }

    // ==== Unity ====

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        InitMaterials();
        GenerateResourceNodes(5);
    }

    void Update()
    {
        HandleHotkeys();
        HandleMouse();

        if (placingConveyor) UpdateGhost(conveyorPrefab);
        if (placingFurnace) UpdateGhost(furnacePrefab);
        if (placingSplitter) UpdateGhost(splitter3Prefab);
        if (placingMiner) UpdateGhost(minerPrefab);

        if (placingItemOnConveyor && itemGhost != null)
        {
            var cell = MouseCell();
            Vector3 p = CellCenter(cell.x, cell.y);
            p.z = ITEM_Z;
            itemGhost.transform.position = p;
        }

        float dt = Time.deltaTime;
        for (int x = 0; x < GridSizeX; x++)
        {
            for (int y = 0; y < GridSizeY; y++)
            {
                var b = Buildings[x, y];
                if (b != null) b.Tick(dt);
            }
        }

        UpdateInventoryValue();
    }

    void HandleHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.B)) ToggleBuildMenu = !ToggleBuildMenu;

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (selected != null && selected.IsRotatable())
            {
                selected.RotateClockwise();
            }
            else if (ghost != null && (placingConveyor || placingSplitter))
            else if (ghost != null && (placingConveyor || placingSplitter || placingMiner))
            {
                ghost.transform.Rotate(0, 0, -90);
            }
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (selected != null) DeleteSelected();
        }

        if (Input.GetMouseButtonDown(1))
        {
            placingConveyor = false;
            placingFurnace = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            ClearGhost();
            if (itemGhost != null) Destroy(itemGhost);
            itemGhost = null;
            ClearSelection();
        }
    }

    void HandleMouse()
    {
        var cell = MouseCell();

        if (Input.GetMouseButtonDown(0))
        {
            if (placingConveyor) { TryPlaceConveyor(cell.x, cell.y); }
            else if (placingFurnace) { TryPlaceFurnace(cell.x, cell.y); }
            else if (placingSplitter) { TryPlaceSplitter(cell.x, cell.y); }
            else if (placingMiner) { TryPlaceMiner(cell.x, cell.y); }
            else if (placingItemOnConveyor) { TryAssignItemToConveyor(cell.x, cell.y); }
            else
            {
                SelectBuildingAt(cell.x, cell.y);
                if (selected != null && selected.x == cell.x && selected.y == cell.y) dragging = true;
            }
        }

        if (dragging && Input.GetMouseButton(0) && selected != null)
        {
            if ((selected.x != cell.x || selected.y != cell.y) && InBounds(cell.x, cell.y))
            {
                if (Buildings[cell.x, cell.y] == null)
                {
                    Buildings[selected.x, selected.y] = null;
                    selected.x = cell.x; selected.y = cell.y;
                    Buildings[cell.x, cell.y] = selected;
                    selected.OnMoved();
                }
            }
        }

        if (Input.GetMouseButtonUp(0)) dragging = false;
    }

@@ -475,66 +612,84 @@ public class Grid_Script : MonoBehaviour
        f.offSprite = furnaceOffSprite;
        f.onSprite = furnaceOnSprite;

        Buildings[x, y] = f;
        f.OnMoved();
    }

    void TryPlaceSplitter(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;

        float z = ghost != null ? ghost.transform.eulerAngles.z : 0f;
        Dir d = AngleToDir(z);

        GameObject go = Instantiate(splitter3Prefab, CellCenter(x, y), Quaternion.Euler(0, 0, z));

        Splitter3 sp = new Splitter3();
        sp.x = x; sp.y = y;
        sp.rot = d;
        sp.go = go;

        Buildings[x, y] = sp;
        sp.OnMoved();
    }

    void TryPlaceMiner(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;

        var node = GetNode(x, y);
        if (node == null) return;

        float z = ghost != null ? ghost.transform.eulerAngles.z : 0f;
        Dir d = AngleToDir(z);

        GameObject go = Instantiate(minerPrefab, CellCenter(x, y), Quaternion.Euler(0, 0, z));

        Miner miner = new Miner();
        miner.x = x; miner.y = y;
        miner.rot = d;
        miner.go = go;
        miner.node = node;
        miner.owner = this;

        Buildings[x, y] = miner;
        miner.OnMoved();
    }

    void TryAssignItemToConveyor(int x, int y)
    {
        if (!InBounds(x, y)) return;

        Conveyor c = Buildings[x, y] as Conveyor;
        if (c == null) return;
        if (!c.CanAccept(null)) return;

        GameObject go = Instantiate(orePrefab, CellCenter(x, y), Quaternion.identity);
        Vector3 p = go.transform.position; p.z = ITEM_Z;
        go.transform.position = p;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 10;

        Item it = new Item("Ore", go);
        MaterialDef mat = materialList.Count > 0 ? materialList[0] : new MaterialDef("debug", Color.white, 1);
        Item it = CreateItem(mat, ItemType.Ore, CellCenter(x, y), mat.baseValue);
        UpdateValueLabel(it);
        c.Accept(it);

        placingItemOnConveyor = false;
        if (itemGhost != null) Destroy(itemGhost);
        itemGhost = null;
    }

    // ==== Auswahl / Bearbeitung ====

    void SelectBuildingAt(int x, int y)
    {
        if (!InBounds(x, y)) 
        { 
            ClearSelection(); 
            return; 
        }

        var b = Buildings[x, y];
        if (b != null)
        {
            if (selected == b) return;
            ClearSelection();
            selected = b;
            ApplySelectionVisual(true);
        }
@@ -574,51 +729,51 @@ public class Grid_Script : MonoBehaviour

    // ==== Hilfen ====

    public static bool InBounds(int x, int y)
    {
        return x >= 0 && x < GridSizeX && y >= 0 && y < GridSizeY;
    }

    (int x, int y) MouseCell()
    {
        var w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        int cx = Mathf.FloorToInt(w.x / CELL_W);
        int cy = Mathf.FloorToInt(w.y / CELL_H);
        return (cx, cy);
    }

    Vector3 CellCenter(int x, int y)
    {
        return new Vector3(x * CELL_W + CELL_W * 0.5f, y * CELL_H + CELL_H * 0.5f, 0);
    }

    static Dir AngleToDir(float z)
    {
        int a = Mathf.RoundToInt(z) % 360;
        if (a < 0) a += 360;
        // 0 Sprite nach oben, 270 nach rechts
        // 0° Sprite nach oben, 270° nach rechts
        switch (a)
        {
            case 0: return Dir.N;
            case 90: return Dir.W;
            case 180: return Dir.S;
            case 270: return Dir.E;
            default: return Dir.N;
        }
    }

    static float DirToAngle(Dir d)
    {
        switch (d)
        {
            case Dir.N: return 0f;
            case Dir.E: return 270f;
            case Dir.S: return 180f;
            case Dir.W: return 90f;
            default: return 0f;
        }
    }

    static (int dx, int dy) StepForDir(Dir d)
    {
        switch (d)
@@ -633,85 +788,259 @@ public class Grid_Script : MonoBehaviour

    static Dir LeftOf(Dir d)
    {
        switch (d)
        {
            case Dir.N: return Dir.W;
            case Dir.W: return Dir.S;
            case Dir.S: return Dir.E;
            case Dir.E: return Dir.N;
            default: return Dir.N;
        }
    }

    static Dir RightOf(Dir d)
    {
        switch (d)
        {
            case Dir.N: return Dir.E;
            case Dir.E: return Dir.S;
            case Dir.S: return Dir.W;
            case Dir.W: return Dir.N;
            default: return Dir.N;
        }
    }

    void InitMaterials()
    {
        materials.Clear();
        materialList.Clear();

        AddMaterial(new MaterialDef("Kupfer", new Color(0.83f, 0.5f, 0.28f, 1f), 1));
        AddMaterial(new MaterialDef("Eisen", new Color(0.75f, 0.75f, 0.77f, 1f), 2));
        AddMaterial(new MaterialDef("Gold", new Color(1f, 0.84f, 0.1f, 1f), 3));
    }

    void AddMaterial(MaterialDef mat)
    {
        materials[mat.id] = mat;
        materialList.Add(mat);
    }

    MaterialDef RandomMaterial()
    {
        if (materialList.Count == 0) return new MaterialDef("Debug", Color.white, 1);
        int idx = UnityEngine.Random.Range(0, materialList.Count);
        return materialList[idx];
    }

    void GenerateResourceNodes(int targetCount)
    {
        int placed = 0;
        int attempts = 0;
        while (placed < targetCount && attempts < targetCount * 20)
        {
            attempts++;
            int x = UnityEngine.Random.Range(1, GridSizeX - 1);
            int y = UnityEngine.Random.Range(1, GridSizeY - 1);

            if (resourceNodes[x, y] != null) continue;
            if (Buildings[x, y] != null) continue;

            var mat = RandomMaterial();
            PlaceResourceNode(x, y, mat);
            placed++;
        }
    }

    void PlaceResourceNode(int x, int y, MaterialDef mat)
    {
        var node = new ResourceNode { x = x, y = y, material = mat };
        if (resourceNodePrefab != null)
        {
            node.go = Instantiate(resourceNodePrefab, CellCenter(x, y), Quaternion.identity);
            var sr = node.go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = mat.color;
                sr.sortingOrder = 5;
            }
        }
        resourceNodes[x, y] = node;
    }

    Item CreateItem(MaterialDef material, ItemType type, Vector3 position, double valueOverride)
    {
        GameObject prefab = type == ItemType.Ingot ? ingotPrefab : orePrefab;
        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        Vector3 p = go.transform.position; p.z = ITEM_Z;
        go.transform.position = p;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = material.color;
            sr.sortingOrder = 10;
        }

        Item item = new Item();
        item.id = material.id;
        item.go = go;
        item.material = material;
        item.type = type;
        item.value = valueOverride;

        CreateValueLabel(item);
        return item;
    }

    void CreateValueLabel(Item item)
    {
        if (item == null || item.go == null) return;

        GameObject label = new GameObject("ValueLabel");
        label.transform.SetParent(item.go.transform);
        label.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        var tm = label.AddComponent<TextMesh>();
        tm.color = new Color(0.6f, 1f, 0.6f, 1f);
        tm.fontSize = 64;
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.text = $"€{item.value:0}";
        item.valueLabel = tm;

        var mr = label.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 20;
    }

    void UpdateValueLabel(Item item)
    {
        if (item == null || item.valueLabel == null) return;
        item.valueLabel.text = $"€{item.value:0}";
    }

    void DestroyItemVisual(Item item)
    {
        if (item == null) return;
        if (item.go != null) Destroy(item.go);
        item.valueLabel = null;
        item.go = null;
    }

    ResourceNode GetNode(int x, int y)
    {
        if (!InBounds(x, y)) return null;
        return resourceNodes[x, y];
    }

    void UpdateInventoryValue()
    {
        double total = 0;
        for (int x = 0; x < GridSizeX; x++)
        {
            for (int y = 0; y < GridSizeY; y++)
            {
                var b = Buildings[x, y];
                if (b != null && b.item != null)
                {
                    total += b.item.value;
                }
            }
        }
        currentInventoryValue = total;
    }

    // ==== Minimal-GUI ====

    bool ToggleBuildMenu;

    void DrawMoneyCounter()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = new Color(0.6f, 1f, 0.6f, 1f);

        string label = $"Gesamtwert: €{currentInventoryValue:0}";
        Vector2 size = style.CalcSize(new GUIContent(label));
        float w = size.x + 10f;
        GUI.Label(new Rect(Screen.width - w - 10f, 10f, w, 30f), label, style);
    }

    void OnGUI()
    {
        DrawMoneyCounter();

        if (!ToggleBuildMenu) return;

        GUI.Box(new Rect(10, 10, 260, 190), "Build");
        GUI.Box(new Rect(10, 10, 260, 220), "Build");
        if (GUI.Button(new Rect(20, 40, 90, 20), "Conveyor"))
        {
            placingConveyor = true;
            placingFurnace = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(120, 40, 90, 20), "Furnace"))
        {
            placingFurnace = true;
            placingConveyor = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(20, 70, 90, 20), "Splitter 3"))
        {
            placingSplitter = true;
            placingConveyor = false;
            placingFurnace = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(120, 70, 90, 20), "Debug Item"))
        {
            placingItemOnConveyor = true;
            placingConveyor = false;
            placingFurnace = false;
            placingSplitter = false;
            placingMiner = false;
            ClearGhost();
            ClearSelection();

            if (itemGhost == null)
            {
                itemGhost = Instantiate(orePrefab, Vector3.zero, Quaternion.identity);
                Vector3 p = itemGhost.transform.position; p.z = ITEM_Z;
                itemGhost.transform.position = p;
                var sr = itemGhost.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;
                if (sr != null)
                {
                    sr.sortingOrder = 10;
                    if (materialList.Count > 0) sr.color = materialList[0].color;
                }
            }
        }

        GUI.Label(new Rect(20, 100, 220, 20), "Klick=Auswahl, R=Rotieren, X=Lschen");
        GUI.Label(new Rect(20, 120, 220, 20), "Drag=Bewegen, Furnace rotiert nicht");
        GUI.Label(new Rect(20, 140, 220, 20), "Splitter: L->F->R Round-Robin");
        if (GUI.Button(new Rect(20, 100, 90, 20), "Miner"))
        {
            placingMiner = true;
            placingConveyor = false;
            placingFurnace = false;
            placingSplitter = false;
            placingItemOnConveyor = false;
            ClearGhost();
            ClearSelection();
        }

        GUI.Label(new Rect(20, 130, 220, 20), "Klick=Auswahl, R=Rotieren, X=Löschen");
        GUI.Label(new Rect(20, 150, 220, 20), "Drag=Bewegen, Furnace rotiert nicht");
        GUI.Label(new Rect(20, 170, 220, 20), "Splitter: L->F->R Round-Robin");
        GUI.Label(new Rect(20, 190, 220, 20), "Miner nur auf Nodes platzierbar");
    }
}