using UnityEngine;
using System;

public class Grid_Script : MonoBehaviour
{
    // ==== Grid ====
    public static int GridSizeX = 26;
    public static int GridSizeY = 15;
    public const float CELL_W = 0.64f;
    public const float CELL_H = 0.64f;
    public const float ITEM_Z = -0.1f;

    public static Building[,] Buildings = new Building[GridSizeX, GridSizeY];

    // ==== Prefabs ====
    public GameObject conveyorPrefab;
    public GameObject orePrefab;
    public GameObject furnacePrefab;
    public GameObject splitter3Prefab;   // 3-Way-Splitter

    // ==== Furnace Sprites ====
    public Sprite furnaceOffSprite;
    public Sprite furnaceOnSprite;

    // ==== Platzierung ====
    GameObject ghost;
    bool placingConveyor;
    bool placingFurnace;
    bool placingSplitter;

    // ==== Debug-Item auf Conveyor setzen ====
    bool placingItemOnConveyor;
    GameObject itemGhost;

    // ==== Auswahl / Bearbeitung ====
    Building selected;
    bool dragging;

    // ==== Datentypen ====
    public enum Dir { N, E, S, W }

    public class Item
    {
        public string id;
        public GameObject go;
        public Item(string id, GameObject go) { this.id = id; this.go = go; }
    }

    public class Building
    {
        public Item item;
        public Dir rot;
        public int x, y;
        public GameObject go;

        public virtual bool CanAccept(Item i) { return item == null; }
        public virtual bool Accept(Item i) { if (!CanAccept(i)) return false; item = i; return true; }
        public virtual void Tick(float dt) { }
        public virtual void OnMoved() { if (go != null) go.transform.position = Center(); }
        public virtual bool IsRotatable() { return false; }
        public virtual void RotateClockwise() { }
        public Vector3 Center() { return new Vector3(x * CELL_W + CELL_W * 0.5f, y * CELL_H + CELL_H * 0.5f, 0); }
    }

    public class Conveyor : Building
    {
        public float speed = 2f;
        float t;
        Vector3 start, end;

        void ResetSegment()
        {
            start = Center();
            Vector2 v = Vector2.zero;
            switch (rot)
            {
                case Dir.N: v = Vector2.up; break;
                case Dir.E: v = Vector2.right; break;
                case Dir.S: v = Vector2.down; break;
                case Dir.W: v = Vector2.left; break;
            }
            end = start + (Vector3)(v * new Vector2(CELL_W, CELL_H));
            t = 0f;
        }

        public void Init()
        {
            ResetSegment();
            if (item != null)
            {
                Vector3 p = start; p.z = ITEM_Z;
                item.go.transform.position = p;
            }
        }

        public override void Tick(float dt)
        {
            if (item == null) return;

            t += dt * speed;
            if (t < 1f)
            {
                Vector3 p = Vector3.Lerp(start, end, t);
                p.z = ITEM_Z;
                item.go.transform.position = p;
                return;
            }

            var next = NextCell();
            if (!InBounds(next.x, next.y))
            {
                t = 1f;
                Vector3 pEdge = end; pEdge.z = ITEM_Z;
                item.go.transform.position = pEdge;
                return;
            }

            var nb = Buildings[next.x, next.y];
            if (nb != null && nb.CanAccept(item))
            {
                nb.Accept(item);
                item = null;
                return;
            }

            t = 1f;
            Vector3 pBlocked = end; pBlocked.z = ITEM_Z;
            item.go.transform.position = pBlocked;
        }

        public (int x, int y) NextCell()
        {
            switch (rot)
            {
                case Dir.N: return (x, y + 1);
                case Dir.E: return (x + 1, y);
                case Dir.S: return (x, y - 1);
                case Dir.W: return (x - 1, y);
                default: return (x, y);
            }
        }

        public override bool Accept(Item i)
        {
            if (!base.Accept(i)) return false;
            ResetSegment();
            Vector3 p = start; p.z = ITEM_Z;
            i.go.transform.position = p;
            return true;
        }

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
            Init();
        }

        public override void OnMoved()
        {
            if (go != null) go.transform.position = Center();
            Init();
        }
    }

    public class Furnace : Building
    {
        public float processTime = 2f;
        float t;
        public Sprite offSprite;
        public Sprite onSprite;

        void UpdateSprite()
        {
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
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
                if (item.go != null) UnityEngine.Object.Destroy(item.go);
                item = null;
                t = 0f;
                UpdateSprite();
            }
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
        {
            if (item == null) return;

            for (int k = 0; k < 3; k++)
            {
                int idx = (rrIndex + k) % 3;
                Dir outDir = DirForIndex(idx); // L,F,R relativ zur aktuellen rot
                var step = StepForDir(outDir);
                int tx = x + step.dx;
                int ty = y + step.dy;

                if (!InBounds(tx, ty)) continue;

                Building nb = Buildings[tx, ty];
                if (nb == null) continue;                // nur ausgeben, wenn Ziel existiert
                if (!nb.CanAccept(item)) continue;       // nur wenn Ziel frei ist

                bool ok = nb.Accept(item);
                if (ok)
                {
                    item = null;
                    rrIndex = (idx + 1) % 3;            // Round-Robin fortsetzen
                    break;
                }
            }

            // Item visuell mittig halten, wenn blockiert
            if (item != null && item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
        }

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

    // ==== Unity ====

    void Update()
    {
        HandleHotkeys();
        HandleMouse();

        if (placingConveyor) UpdateGhost(conveyorPrefab);
        if (placingFurnace) UpdateGhost(furnacePrefab);
        if (placingSplitter) UpdateGhost(splitter3Prefab);

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

    // ==== Platzierung ====

    void UpdateGhost(GameObject prefab)
    {
        var p = CellCenter(MouseCell().x, MouseCell().y);
        if (ghost == null) ghost = Instantiate(prefab, p, Quaternion.identity);
        ghost.transform.position = p;
    }

    void ClearGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
    }

    void TryPlaceConveyor(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;

        float z = ghost != null ? ghost.transform.eulerAngles.z : 0f;
        Dir d = AngleToDir(z);

        GameObject go = Instantiate(conveyorPrefab, CellCenter(x, y), Quaternion.Euler(0, 0, z));

        Conveyor conv = new Conveyor();
        conv.x = x; conv.y = y;
        conv.rot = d;
        conv.go = go;

        Buildings[x, y] = conv;
        conv.Init();
    }

    void TryPlaceFurnace(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;

        GameObject go = Instantiate(furnacePrefab, CellCenter(x, y), Quaternion.identity);

        Furnace f = new Furnace();
        f.x = x; f.y = y;
        f.rot = Dir.N;
        f.go = go;
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
        c.Accept(it);

        placingItemOnConveyor = false;
        if (itemGhost != null) Destroy(itemGhost);
        itemGhost = null;
    }

    // ==== Auswahl / Bearbeitung ====

    void SelectBuildingAt(int x, int y)
    {
        if (!InBounds(x, y)) { ClearSelection(); return; }

        var b = Buildings[x, y];
        if (b != null)
        {
            if (selected == b) return;
            ClearSelection();
            selected = b;
            ApplySelectionVisual(true);
        }
        else ClearSelection();
    }

    void ClearSelection()
    {
        if (selected != null) ApplySelectionVisual(false);
        selected = null;
    }

    void ApplySelectionVisual(bool on)
    {
        if (selected == null || selected.go == null) return;
        var sr = selected.go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = on ? new Color(1f, 1f, 0.6f, 1f) : Color.white;
    }

    void DeleteSelected()
    {
        if (selected == null) return;

        if (selected.item != null && selected.item.go != null)
        {
            Destroy(selected.item.go);
            selected.item = null;
        }

        if (selected.go != null) Destroy(selected.go);

        if (InBounds(selected.x, selected.y) && Buildings[selected.x, selected.y] == selected)
            Buildings[selected.x, selected.y] = null;

        selected = null;
    }

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
        {
            case Dir.N: return (0, 1);
            case Dir.E: return (1, 0);
            case Dir.S: return (0, -1);
            case Dir.W: return (-1, 0);
            default: return (0, 0);
        }
    }

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

    // ==== Minimal-GUI ====

    bool ToggleBuildMenu;

    void OnGUI()
    {
        if (!ToggleBuildMenu) return;

        GUI.Box(new Rect(10, 10, 260, 190), "Build");
        if (GUI.Button(new Rect(20, 40, 90, 20), "Conveyor"))
        {
            placingConveyor = true;
            placingFurnace = false;
            placingSplitter = false;
            placingItemOnConveyor = false;
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(120, 40, 90, 20), "Furnace"))
        {
            placingFurnace = true;
            placingConveyor = false;
            placingSplitter = false;
            placingItemOnConveyor = false;
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(20, 70, 90, 20), "Splitter 3"))
        {
            placingSplitter = true;
            placingConveyor = false;
            placingFurnace = false;
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
            ClearGhost();
            ClearSelection();

            if (itemGhost == null)
            {
                itemGhost = Instantiate(orePrefab, Vector3.zero, Quaternion.identity);
                Vector3 p = itemGhost.transform.position; p.z = ITEM_Z;
                itemGhost.transform.position = p;
                var sr = itemGhost.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;
            }
        }

        GUI.Label(new Rect(20, 100, 220, 20), "Klick=Auswahl, R=Rotieren, X=Löschen");
        GUI.Label(new Rect(20, 120, 220, 20), "Drag=Bewegen, Furnace rotiert nicht");
        GUI.Label(new Rect(20, 140, 220, 20), "Splitter: L?F?R Round-Robin");
    }
}
