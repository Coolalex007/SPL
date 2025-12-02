using UnityEngine;
using System;
using System.Collections.Generic;

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
    public GameObject ingotPrefab;
    public GameObject furnacePrefab;
    public GameObject forgePrefab;
    public GameObject alloyFurnacePrefab;
    public GameObject splitter3Prefab;   // 3-Way-Splitter
    public GameObject minerPrefab;
    public GameObject nodePrefab;
    public GameObject sellerPrefab;
    public GameObject platePrefab;
    public GameObject boltPrefab;
    public GameObject reinforcedPlatePrefab;
    public GameObject craftingTablePrefab;
    public GameObject directionArrowPrefab;

    Dictionary<int, Sprite> alloyHalfLeftCache = new Dictionary<int, Sprite>();
    Dictionary<int, Sprite> alloyHalfRightCache = new Dictionary<int, Sprite>();

    // ==== Furnace Sprites ====
    public Sprite furnaceOffSprite;
    public Sprite furnaceOnSprite;

    // ==== Platzierung ====
    GameObject ghost;
    GameObject ghostDirectionArrow;
    bool placingConveyor;
    bool placingFurnace;
    bool placingSplitter;
    bool placingMiner;
    bool placingSeller;
    bool placingForge;
    bool placingAlloyFurnace;
    bool placingCraftingTable;
    Dir placementDir = Dir.N;

    // ==== Debug-Item auf Conveyor setzen ====
    bool placingItemOnConveyor;
    GameObject itemGhost;

    // ==== Auswahl / Bearbeitung ====
    Building selected;
    bool dragging;
    Rect? selectedUIRect;

    // ==== Datentypen ====
    public enum Dir { N, E, S, W }

    public enum ItemForm
    {
        Ore,
        Ingot,
        Plate,
        Bolt,
        ReinforcedPlate
    }

    public enum ResourceType
    {
        Copper,
        Iron,
        Gold
    }

    public class Item
    {
        public string id;
        public ResourceType resource;
        public ResourceType? secondaryResource;
        public int value;
        public ItemForm form;
        public GameObject go;
        public TextMesh valueLabel;
        public bool isAlloy;

        public Item(string id, ResourceType resource, int value, ItemForm form, GameObject go)
        {
            this.id = id;
            this.resource = resource;
            this.value = value;
            this.form = form;
            this.go = go;
        }
    }

    public class Node
    {
        public ResourceType resource;
        public GameObject go;
    }

    public class Building
    {
        public Item item;
        public Dir rot;
        public int x, y;
        public GameObject go;
        public Grid_Script grid;

        protected GameObject directionArrow;

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

        public override bool CanAccept(Item i)
        {
            if (i == null) return false;
            if (i.form != ItemForm.Ore) return false; // nur Erze schmelzen
            return base.CanAccept(i);
        }

        void UpdateSprite()
        {
            if (go == null) return;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.sprite = (item != null) ? onSprite : offSprite;
        }

        void UpdateDirection()
        {
            if (grid == null || go == null) return;
            grid.UpdateDirectionArrow(ref directionArrow, go.transform, rot);
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
            UpdateDirection();
            return true;
        }

        public override void Tick(float dt)
        {
            if (item == null) return;
            t += dt;
            if (t >= processTime)
            {
                Item ingot = grid != null ? grid.CreateItemFromResource(item.resource, ItemForm.Ingot) : null;
                if (item.go != null) UnityEngine.Object.Destroy(item.go);

                if (ingot != null)
                {
                    ingot.go.transform.position = Center();
                    item = ingot;
                }
                else
                {
                    item.form = ItemForm.Ingot;
                    item.value += 1;
                    item.id = item.resource.ToString() + " Ingot";
                    if (grid != null)
                    {
                        grid.ColorizeItem(item);
                        grid.UpdateItemValueLabel(item);
                    }
                }
                AttemptOutput();
                t = 0f;
                UpdateSprite();
            }
        }

        void AttemptOutput()
        {
            if (item == null) return;

            var step = StepForDir(rot);
            int tx = x + step.dx;
            int ty = y + step.dy;
            if (!InBounds(tx, ty)) return;

            var nb = Buildings[tx, ty];
            if (nb != null && nb.CanAccept(item))
            {
                nb.Accept(item);
                item = null;
            }
            else if (item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
        }

        public override void OnMoved()
        {
            base.OnMoved();
            UpdateSprite();
            UpdateDirection();
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
            UpdateDirection();
        }
    }

    public class AlloyFurnace : Building
    {
        public float processTime = 2f;
        float t;
        List<Item> inputs = new List<Item>();

        void UpdateDirection()
        {
            if (grid == null || go == null) return;
            grid.UpdateDirectionArrow(ref directionArrow, go.transform, rot);
        }

        public override bool CanAccept(Item i)
        {
            if (i == null) return false;
            if (item != null) return false; // output slot occupied
            if (inputs.Count >= 2) return false;
            if (i.form != ItemForm.Ingot) return false;
            if (i.isAlloy) return false; // prevent re-alloying
            return true;
        }

        public override bool Accept(Item i)
        {
            if (!CanAccept(i)) return false;
            inputs.Add(i);
            RepositionInputs();
            UpdateDirection();
            return true;
        }

        public override void Tick(float dt)
        {
            if (item != null)
            {
                AttemptOutput();
                return;
            }

            if (inputs.Count < 2) return;

            t += dt;
            if (t >= processTime)
            {
                ProcessAlloy();
                t = 0f;
            }
        }

        void ProcessAlloy()
        {
            if (inputs.Count < 2) return;

            Item first = inputs[0];
            Item second = inputs[1];
            inputs.Clear();

            int combinedValue = first.value + second.value;
            ResourceType primary = first.resource;
            ResourceType secondary = second.resource;

            ConsumeItem(first);
            ConsumeItem(second);

            Item alloy = grid != null ? grid.CreateAlloyItem(primary, secondary, combinedValue) : null;
            if (alloy != null)
            {
                alloy.go.transform.position = Center();
                item = alloy;
            }
            else
            {
                GameObject go = new GameObject("Alloy");
                go.transform.position = Center();
                Item fallback = new Item(primary.ToString() + "-" + secondary.ToString() + " Alloy", primary, combinedValue, ItemForm.Ingot, go);
                fallback.isAlloy = true;
                fallback.secondaryResource = secondary;
                item = fallback;
                if (grid != null)
                {
                    grid.ColorizeItem(item);
                    grid.CreateItemValueLabel(item);
                    grid.UpdateItemValueLabel(item);
                }
            }

            AttemptOutput();
            RepositionInputs();
        }

        void ConsumeItem(Item it)
        {
            if (it == null) return;
            if (it.go != null) UnityEngine.Object.Destroy(it.go);
        }

        void AttemptOutput()
        {
            if (item == null) return;

            var step = StepForDir(rot);
            int tx = x + step.dx;
            int ty = y + step.dy;
            if (!InBounds(tx, ty)) return;

            var nb = Buildings[tx, ty];
            if (nb != null && nb.CanAccept(item))
            {
                nb.Accept(item);
                item = null;
            }
            else if (item != null && item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
        }

        void RepositionInputs()
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].go == null) continue;
                Vector3 p = Center();
                p.z = ITEM_Z;
                p.x += (i - 0.5f) * 0.12f;
                inputs[i].go.transform.position = p;
                var sr = inputs[i].go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10 + i;
            }
        }

        public void ClearContents()
        {
            if (item != null && item.go != null) UnityEngine.Object.Destroy(item.go);
            item = null;

            foreach (var input in inputs)
            {
                if (input != null && input.go != null) UnityEngine.Object.Destroy(input.go);
            }
            inputs.Clear();
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
            UpdateDirection();
        }

        public override void OnMoved()
        {
            base.OnMoved();
            RepositionInputs();
            if (item != null && item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
            UpdateDirection();
        }
    }

    public class Forge : Building
    {
        public float processTime = 2f;
        float t;

        public enum ForgeRecipe
        {
            IngotToPlate,
            IngotToBolt
        }

        public ForgeRecipe recipe = ForgeRecipe.IngotToPlate;

        public override bool CanAccept(Item i)
        {
            if (i == null) return false;
            if (i.form != ItemForm.Ingot) return false; // nur Barren annehmen
            return base.CanAccept(i);
        }

        void UpdateDirection()
        {
            if (grid == null || go == null) return;
            grid.UpdateDirectionArrow(ref directionArrow, go.transform, rot);
        }

        public override bool Accept(Item i)
        {
            if (!base.Accept(i)) return false;
            t = 0f;
            if (i.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                i.go.transform.position = p;
                var sr = i.go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10;
            }
            UpdateDirection();
            return true;
        }

        public override void Tick(float dt)
        {
            if (item == null) return;

            if (item.form != ItemForm.Ingot)
            {
                AttemptOutput();
                return;
            }

            t += dt;
            if (t >= processTime)
            {
                ProcessRecipe();
                t = 0f;
            }
        }

        void ProcessRecipe()
        {
            ItemForm targetForm = recipe == ForgeRecipe.IngotToPlate ? ItemForm.Plate : ItemForm.Bolt;
            Item result = grid != null ? grid.CreateItemFromResource(item.resource, targetForm) : null;
            if (item.go != null) UnityEngine.Object.Destroy(item.go);

            if (result != null)
            {
                int valueDelta = recipe == ForgeRecipe.IngotToPlate ? 2 : 1;
                result.value = item.value + valueDelta;
                result.id = item.resource.ToString() + (recipe == ForgeRecipe.IngotToPlate ? " Plate" : " Bolt");
                if (grid != null)
                {
                    grid.UpdateItemValueLabel(result);
                }
                result.go.transform.position = Center();
                item = result;
            }
            else
            {
                item.form = targetForm;
                item.value += recipe == ForgeRecipe.IngotToPlate ? 2 : 1;
                item.id = item.resource.ToString() + (recipe == ForgeRecipe.IngotToPlate ? " Plate" : " Bolt");
                if (grid != null)
                {
                    grid.ColorizeItem(item);
                    grid.UpdateItemValueLabel(item);
                }
            }

            AttemptOutput();
        }

        void AttemptOutput()
        {
            if (item == null) return;

            var step = StepForDir(rot);
            int tx = x + step.dx;
            int ty = y + step.dy;
            if (!InBounds(tx, ty)) return;

            var nb = Buildings[tx, ty];
            if (nb != null && nb.CanAccept(item))
            {
                nb.Accept(item);
                item = null;
            }
            else if (item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
        }

        public override void OnMoved()
        {
            base.OnMoved();
            UpdateDirection();
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
            UpdateDirection();
        }
    }

    public class Miner : Building
    {
        public float mineTime = 2f;
        float t;
        public Node node;

        public override bool CanAccept(Item i) { return false; }

        void UpdateDirection()
        {
            if (grid == null || go == null) return;
            grid.UpdateDirectionArrow(ref directionArrow, go.transform, rot);
        }


        public override void Tick(float dt)
        {
            if (node == null) return;

            if (item != null)
            {
                TryOutput();
                return;
            }

            t += dt;
            if (t < mineTime) return;

            Item ore = grid != null ? grid.CreateItemFromResource(node.resource, ItemForm.Ore) : null;
            if (ore == null) return;
            ore.go.transform.position = Center();
            item = ore;
            TryOutput();
            if (item == null) t = 0f;
            UpdateDirection();
        }

        void TryOutput()
        {
            if (item == null) return;
            var step = StepForDir(rot);
            int tx = x + step.dx;
            int ty = y + step.dy;
            if (!InBounds(tx, ty)) return;

            var nb = Buildings[tx, ty];
            if (nb != null && nb.CanAccept(item))
            {
                nb.Accept(item);
                item = null;
            }
            else if (item.go != null)
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
            UpdateDirection();
        }

        public override void OnMoved()
        {
            base.OnMoved();
            if (item != null && item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
            UpdateDirection();
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

    public class Seller : Building
    {
        public override bool Accept(Item i)
        {
            if (i == null) return false;
            if (!base.Accept(i)) return false;

            if (grid != null)
            {
                grid.AddMoney(i.value);
            }

            if (i.go != null) UnityEngine.Object.Destroy(i.go);
            item = null;
            return true;
        }
    }

    public class CraftingTable : Building
    {
        List<Item> inputs = new List<Item>();

        void UpdateDirection()
        {
            if (grid == null || go == null) return;
            grid.UpdateDirectionArrow(ref directionArrow, go.transform, rot);
        }

        public override bool CanAccept(Item i)
        {
            if (i == null) return false;
            if (item != null) return false; // Output slot occupied
            if (i.form != ItemForm.Bolt && i.form != ItemForm.Plate) return false;
            var (bolts, plates) = InputCounts();
            if (i.form == ItemForm.Bolt && bolts >= 2) return false;
            if (i.form == ItemForm.Plate && plates >= 1) return false;
            if (inputs.Count > 0 && inputs[0].resource != i.resource) return false;
            return true;
        }

        public override bool Accept(Item i)
        {
            if (!CanAccept(i)) return false;
            inputs.Add(i);
            RepositionInputs();
            UpdateDirection();
            return true;
        }

        public override void Tick(float dt)
        {
            if (item != null)
            {
                AttemptOutput();
                return;
            }

            TryCraft();
        }

        void TryCraft()
        {
            int boltCount = 0;
            int plateCount = 0;
            foreach (var it in inputs)
            {
                if (it.form == ItemForm.Bolt) boltCount++;
                else if (it.form == ItemForm.Plate) plateCount++;
            }

            if (boltCount < 2 || plateCount < 1) return;

            if (inputs.Count == 0) return;
            ResourceType res = inputs[0].resource;
            foreach (var it in inputs)
            {
                if (it.resource != res) return; // require matching resources
            }

            Item boltA = RemoveInput(ItemForm.Bolt);
            Item boltB = RemoveInput(ItemForm.Bolt);
            Item plate = RemoveInput(ItemForm.Plate);

            if (boltA == null || boltB == null || plate == null) return;

            int inputValue = boltA.value + boltB.value + plate.value;
            int outputValue = inputValue * 2;

            ConsumeItem(boltA);
            ConsumeItem(boltB);
            ConsumeItem(plate);

            Item reinforced = grid != null ? grid.CreateItemFromResource(res, ItemForm.ReinforcedPlate) : null;
            if (reinforced != null)
            {
                reinforced.value = outputValue;
                reinforced.id = res.ToString() + " Reinforced Plate";
                if (grid != null) grid.UpdateItemValueLabel(reinforced);
                reinforced.go.transform.position = Center();
                item = reinforced;
            }
            else
            {
                GameObject go = new GameObject("ReinforcedPlate");
                go.transform.position = Center();
                Item fallback = new Item(res.ToString() + " Reinforced Plate", res, outputValue, ItemForm.ReinforcedPlate, go);
                item = fallback;
                if (grid != null)
                {
                    grid.ColorizeItem(item);
                    grid.CreateItemValueLabel(item);
                }
            }

            AttemptOutput();
            RepositionInputs();
        }

        Item RemoveInput(ItemForm form)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].form == form)
                {
                    Item it = inputs[i];
                    inputs.RemoveAt(i);
                    return it;
                }
            }
            return null;
        }

        public (int bolts, int plates) InputCounts()
        {
            int bolts = 0;
            int plates = 0;
            foreach (var it in inputs)
            {
                if (it.form == ItemForm.Bolt) bolts++;
                else if (it.form == ItemForm.Plate) plates++;
            }
            return (bolts, plates);
        }

        public void ClearContents()
        {
            if (item != null && item.go != null)
            {
                UnityEngine.Object.Destroy(item.go);
            }
            item = null;

            foreach (var it in inputs)
            {
                if (it != null && it.go != null)
                {
                    UnityEngine.Object.Destroy(it.go);
                }
            }
            inputs.Clear();
        }

        void ConsumeItem(Item it)
        {
            if (it == null) return;
            if (it.go != null) UnityEngine.Object.Destroy(it.go);
        }

        void AttemptOutput()
        {
            if (item == null) return;

            var step = StepForDir(rot);
            int tx = x + step.dx;
            int ty = y + step.dy;
            if (!InBounds(tx, ty)) return;

            var nb = Buildings[tx, ty];
            if (nb != null && nb.CanAccept(item))
            {
                nb.Accept(item);
                item = null;
            }
            else if (item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
        }

        void RepositionInputs()
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                if (inputs[i].go == null) continue;
                Vector3 p = Center();
                p.z = ITEM_Z;
                p.x += (i - 1) * 0.1f;
                inputs[i].go.transform.position = p;
                var sr = inputs[i].go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sortingOrder = 10 + i;
            }
        }

        public override bool IsRotatable() { return false; }

        public override void OnMoved()
        {
            base.OnMoved();
            RepositionInputs();
            if (item != null && item.go != null)
            {
                Vector3 p = Center(); p.z = ITEM_Z;
                item.go.transform.position = p;
            }
            UpdateDirection();
        }
    }

    Node[,] nodes = new Node[GridSizeX, GridSizeY];
    float totalMoney;

    // ==== Unity ====

    void Start()
    {
        GenerateResourceNodes(5);
    }

    void Update()
    {
        HandleHotkeys();
        HandleMouse();

        if (placingConveyor) UpdateGhost(conveyorPrefab);
        if (placingFurnace) UpdateGhost(furnacePrefab);
        if (placingAlloyFurnace && (alloyFurnacePrefab != null || furnacePrefab != null)) UpdateGhost(alloyFurnacePrefab ?? furnacePrefab);
        if (placingSplitter) UpdateGhost(splitter3Prefab);
        if (placingMiner) UpdateGhost(minerPrefab);
        if (placingSeller) UpdateGhost(sellerPrefab);
        if (placingForge) UpdateGhost(forgePrefab);
        if (placingCraftingTable) UpdateGhost(craftingTablePrefab);

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
            else if (placingConveyor || placingSplitter || placingMiner || placingFurnace || placingAlloyFurnace || placingForge || placingCraftingTable)
            {
                RotatePlacementDirection();
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
            placingForge = false;
            placingSplitter = false;
            placingMiner = false;
            placingSeller = false;
            placingAlloyFurnace = false;
            placingCraftingTable = false;
            placingItemOnConveyor = false;
            ResetPlacementDirection();
            ClearGhost();
            if (itemGhost != null) Destroy(itemGhost);
            itemGhost = null;
            ClearSelection();
        }
    }

    void HandleMouse()
    {
        bool mouseOverSelectedUI = IsMouseOverSelectedUI();

        if (mouseOverSelectedUI && (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0)))
        {
            dragging = false;
            return;
        }

        var cell = MouseCell();

        if (Input.GetMouseButtonDown(0))
        {
            if (placingConveyor) { TryPlaceConveyor(cell.x, cell.y); return; }
            if (placingFurnace) { TryPlaceFurnace(cell.x, cell.y); return; }
            if (placingAlloyFurnace) { TryPlaceAlloyFurnace(cell.x, cell.y); return; }
            if (placingForge) { TryPlaceForge(cell.x, cell.y); return; }
            if (placingSplitter) { TryPlaceSplitter(cell.x, cell.y); return; }
            if (placingMiner) { TryPlaceMiner(cell.x, cell.y); return; }
            if (placingSeller) { TryPlaceSeller(cell.x, cell.y); return; }
            if (placingCraftingTable) { TryPlaceCraftingTable(cell.x, cell.y); return; }
            if (placingItemOnConveyor) { TryAssignItemToConveyor(cell.x, cell.y); return; }

            SelectBuildingAt(cell.x, cell.y);
            if (selected != null && selected.x == cell.x && selected.y == cell.y) dragging = true;
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

        if (placingConveyor || placingSplitter)
        {
            ghost.transform.rotation = Quaternion.Euler(0, 0, DirToAngle(placementDir));
            ClearGhostDirectionArrow();
        }
        else if (placingFurnace || placingAlloyFurnace || placingMiner || placingForge || placingCraftingTable)
        {
            ghost.transform.rotation = Quaternion.identity;
            UpdateGhostDirectionArrow();
        }
        else
        {
            ClearGhostDirectionArrow();
        }
    }

    void ClearGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        ClearGhostDirectionArrow();
    }

    void ClearGhostDirectionArrow()
    {
        if (ghostDirectionArrow != null) Destroy(ghostDirectionArrow);
        ghostDirectionArrow = null;
    }

    void UpdateGhostDirectionArrow()
    {
        if (ghost == null) return;
        UpdateDirectionArrow(ref ghostDirectionArrow, ghost.transform, placementDir);
    }

    void RotatePlacementDirection()
    {
        placementDir = RightOf(placementDir);

        if (ghost != null)
        {
            if (placingConveyor || placingSplitter)
            {
                ghost.transform.rotation = Quaternion.Euler(0, 0, DirToAngle(placementDir));
            }
            else if (placingFurnace || placingAlloyFurnace || placingMiner || placingForge || placingCraftingTable)
            {
                UpdateGhostDirectionArrow();
            }
        }
    }

    void ResetPlacementDirection()
    {
        placementDir = Dir.N;
    }

    void TryPlaceConveyor(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;

        Dir d = placementDir;

        GameObject go = Instantiate(conveyorPrefab, CellCenter(x, y), Quaternion.Euler(0, 0, DirToAngle(placementDir)));

        Conveyor conv = new Conveyor();
        conv.x = x; conv.y = y;
        conv.rot = d;
        conv.go = go;
        conv.grid = this;

        Buildings[x, y] = conv;
        conv.Init();
    }

    void TryPlaceFurnace(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;
        if (furnacePrefab == null) return;

        Dir d = placementDir;

        GameObject go = Instantiate(furnacePrefab, CellCenter(x, y), Quaternion.identity);

        Furnace f = new Furnace();
        f.x = x; f.y = y;
        f.rot = d;
        f.go = go;
        f.offSprite = furnaceOffSprite;
        f.onSprite = furnaceOnSprite;
        f.grid = this;

        Buildings[x, y] = f;
        f.OnMoved();
    }

    void TryPlaceAlloyFurnace(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;
        if (alloyFurnacePrefab == null && furnacePrefab == null) return;

        Dir d = placementDir;

        GameObject prefab = alloyFurnacePrefab != null ? alloyFurnacePrefab : furnacePrefab;
        GameObject go = Instantiate(prefab, CellCenter(x, y), Quaternion.identity);

        AlloyFurnace f = new AlloyFurnace();
        f.x = x; f.y = y;
        f.rot = d;
        f.go = go;
        f.grid = this;

        Buildings[x, y] = f;
        f.OnMoved();
    }

    void TryPlaceForge(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;
        if (forgePrefab == null) return;

        Dir d = placementDir;

        GameObject go = Instantiate(forgePrefab, CellCenter(x, y), Quaternion.identity);

        Forge f = new Forge();
        f.x = x; f.y = y;
        f.rot = d;
        f.go = go;
        f.grid = this;

        Buildings[x, y] = f;
        f.OnMoved();
    }

    void TryPlaceCraftingTable(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;
        if (craftingTablePrefab == null) return;

        Dir d = placementDir;

        GameObject go = Instantiate(craftingTablePrefab, CellCenter(x, y), Quaternion.identity);

        CraftingTable table = new CraftingTable();
        table.x = x; table.y = y;
        table.rot = d;
        table.go = go;
        table.grid = this;

        Buildings[x, y] = table;
        table.OnMoved();
    }

    void TryPlaceSplitter(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;

        Dir d = placementDir;

        GameObject go = Instantiate(splitter3Prefab, CellCenter(x, y), Quaternion.Euler(0, 0, DirToAngle(placementDir)));

        Splitter3 sp = new Splitter3();
        sp.x = x; sp.y = y;
        sp.rot = d;
        sp.go = go;

        Buildings[x, y] = sp;
        sp.OnMoved();
    }

    void TryPlaceSeller(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;

        GameObject go = Instantiate(sellerPrefab, CellCenter(x, y), Quaternion.identity);

        Seller s = new Seller();
        s.x = x; s.y = y;
        s.rot = Dir.N;
        s.go = go;
        s.grid = this;

        Buildings[x, y] = s;
        s.OnMoved();
    }

    void TryPlaceMiner(int x, int y)
    {
        if (!InBounds(x, y) || Buildings[x, y] != null) return;
        if (nodes[x, y] == null) return;

        Dir d = placementDir;

        GameObject go = Instantiate(minerPrefab, CellCenter(x, y), Quaternion.identity);

        Miner m = new Miner();
        m.x = x; m.y = y;
        m.rot = d;
        m.go = go;
        m.node = nodes[x, y];
        m.grid = this;

        Buildings[x, y] = m;
        m.OnMoved();
    }

    void TryAssignItemToConveyor(int x, int y)
    {
        if (!InBounds(x, y)) return;

        Conveyor c = Buildings[x, y] as Conveyor;
        if (c == null) return;
        if (!c.CanAccept(null)) return;

        Item it = CreateItemFromResource(ResourceType.Copper, ItemForm.Ore);
        it.go.transform.position = CellCenter(x, y);
        Vector3 p = it.go.transform.position; p.z = ITEM_Z;
        it.go.transform.position = p;
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

        if (selected is CraftingTable crafting)
        {
            crafting.ClearContents();
        }
        else if (selected is AlloyFurnace alloy)
        {
            alloy.ClearContents();
        }
        else if (selected.item != null && selected.item.go != null)
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

    void GenerateResourceNodes(int count)
    {
        System.Random rng = new System.Random();
        int attempts = 0;
        int placed = 0;
        while (placed < count && attempts < 200)
        {
            attempts++;
            int x = rng.Next(1, GridSizeX - 1);
            int y = rng.Next(1, GridSizeY - 1);
            if (nodes[x, y] != null) continue;

            ResourceType res = (ResourceType)rng.Next(0, 3);
            Node n = new Node { resource = res };
            nodes[x, y] = n;

            if (nodePrefab != null)
            {
                n.go = Instantiate(nodePrefab, CellCenter(x, y), Quaternion.identity);
                ColorizeSprite(n.go, res, ItemForm.Ore);
            }

            placed++;
        }
    }

    Item CreateItemFromResource(ResourceType res, ItemForm form)
    {
        GameObject prefab = null;
        switch (form)
        {
            case ItemForm.Ore: prefab = orePrefab; break;
            case ItemForm.Ingot: prefab = ingotPrefab; break;
            case ItemForm.Plate: prefab = platePrefab ?? ingotPrefab; break;
            case ItemForm.Bolt: prefab = boltPrefab ?? ingotPrefab; break;
            case ItemForm.ReinforcedPlate: prefab = reinforcedPlatePrefab ?? platePrefab ?? ingotPrefab; break;
        }
        if (prefab == null) return null;
        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        Vector3 p = go.transform.position; p.z = ITEM_Z;
        go.transform.position = p;

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 10;

        int baseValue = res == ResourceType.Copper ? 1 : (res == ResourceType.Iron ? 2 : 3);
        int value = baseValue;
        switch (form)
        {
            case ItemForm.Ingot: value = baseValue + 1; break;
            case ItemForm.Plate: value = baseValue + 3; break;
            case ItemForm.Bolt: value = baseValue + 2; break;
            case ItemForm.ReinforcedPlate: value = baseValue + 5; break;
            default: value = baseValue; break;
        }
        string suffix = "";
        switch (form)
        {
            case ItemForm.Ore: suffix = " Ore"; break;
            case ItemForm.Ingot: suffix = " Ingot"; break;
            case ItemForm.Plate: suffix = " Plate"; break;
            case ItemForm.Bolt: suffix = " Bolt"; break;
            case ItemForm.ReinforcedPlate: suffix = " Reinforced Plate"; break;
        }
        Item item = new Item(res.ToString() + suffix, res, value, form, go);
        ColorizeItem(item);
        CreateItemValueLabel(item);
        return item;
    }

    Item CreateAlloyItem(ResourceType primary, ResourceType secondary, int value)
    {
        Item alloy = CreateItemFromResource(primary, ItemForm.Ingot);
        if (alloy == null) return null;

        alloy.value = value;
        alloy.id = primary.ToString() + "-" + secondary.ToString() + " Alloy";
        alloy.isAlloy = true;
        alloy.secondaryResource = secondary;
        UpdateItemValueLabel(alloy);
        ColorizeItem(alloy);
        return alloy;
    }

    void CreateItemValueLabel(Item item)
    {
        if (item == null || item.go == null) return;
        GameObject labelGo = new GameObject("ValueLabel");
        labelGo.transform.SetParent(item.go.transform);
        labelGo.transform.localPosition = new Vector3(0, 0, -0.01f);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = "€" + item.value;
        tm.fontSize = 48;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.7f, 1f, 0.7f, 1f);
        tm.characterSize = 0.05f;
        var mr = labelGo.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingOrder = 20;
            mr.sortingLayerName = "Default";
        }
        item.valueLabel = tm;
    }

    void UpdateItemValueLabel(Item item)
    {
        if (item == null || item.valueLabel == null) return;
        item.valueLabel.text = "€" + item.value;
    }

    void ColorizeItem(Item item)
    {
        if (item == null || item.go == null) return;

        if (item.isAlloy && item.secondaryResource.HasValue)
        {
            ApplyAlloyColors(item);
            return;
        }

        ColorizeSprite(item.go, item.resource, item.form);
    }

    Color ColorForResource(ResourceType res, ItemForm form)
    {
        bool plateLike = form == ItemForm.Plate || form == ItemForm.Bolt || form == ItemForm.ReinforcedPlate;

        Color oreColor = Color.white;
        Color ingotColor = Color.white;
        Color plateColor = Color.white;

        switch (res)
        {
            case ResourceType.Copper:
                oreColor = new Color(0.95f, 0.5f, 0.3f, 1f);
                ingotColor = new Color(1f, 0.65f, 0.4f, 1f);
                plateColor = new Color(1f, 0.4f, 0.4f, 1f);
                break;
            case ResourceType.Iron:
                oreColor = new Color(0.65f, 0.65f, 0.7f, 1f);
                ingotColor = new Color(0.8f, 0.8f, 0.85f, 1f);
                plateColor = new Color(0.85f, 0.75f, 0.85f, 1f);
                break;
            case ResourceType.Gold:
                oreColor = new Color(0.95f, 0.8f, 0.25f, 1f);
                ingotColor = new Color(1f, 0.9f, 0.4f, 1f);
                plateColor = new Color(1f, 0.75f, 0.3f, 1f);
                break;
        }

        if (form == ItemForm.Ore) return oreColor;
        if (form == ItemForm.Ingot) return ingotColor;
        if (plateLike) return plateColor;
        return Color.white;
    }

    void ColorizeSprite(GameObject go, ResourceType res, ItemForm form)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        sr.enabled = true;
        sr.color = ColorForResource(res, form);
    }

    void ApplyAlloyColors(Item item)
    {
        var baseSr = item.go.GetComponent<SpriteRenderer>();
        if (baseSr == null) return;

        Sprite sprite = baseSr.sprite;
        int sortingOrder = baseSr.sortingOrder;
        int sortingLayerId = baseSr.sortingLayerID;
        baseSr.enabled = false;

        Color primaryColor = ColorForResource(item.resource, ItemForm.Ingot);
        Color secondaryColor = ColorForResource(item.secondaryResource.Value, ItemForm.Ingot);

        Sprite leftSprite = CreateHalfSprite(sprite, true);
        Sprite rightSprite = CreateHalfSprite(sprite, false);
        if (leftSprite == null || rightSprite == null) return;

        float halfOffset = sprite.bounds.extents.x * 0.5f;

        Transform left = item.go.transform.Find("AlloyHalfA");
        if (left == null)
        {
            GameObject half = new GameObject("AlloyHalfA");
            half.transform.SetParent(item.go.transform);
            left = half.transform;
        }
        var leftSr = left.GetComponent<SpriteRenderer>();
        if (leftSr == null) leftSr = left.gameObject.AddComponent<SpriteRenderer>();
        leftSr.sprite = leftSprite;
        leftSr.color = primaryColor;
        leftSr.sortingOrder = sortingOrder;
        leftSr.sortingLayerID = sortingLayerId;
        left.localPosition = new Vector3(-halfOffset, 0f, 0f);
        left.localScale = Vector3.one;

        Transform right = item.go.transform.Find("AlloyHalfB");
        if (right == null)
        {
            GameObject half = new GameObject("AlloyHalfB");
            half.transform.SetParent(item.go.transform);
            right = half.transform;
        }
        var rightSr = right.GetComponent<SpriteRenderer>();
        if (rightSr == null) rightSr = right.gameObject.AddComponent<SpriteRenderer>();
        rightSr.sprite = rightSprite;
        rightSr.color = secondaryColor;
        rightSr.sortingOrder = sortingOrder;
        rightSr.sortingLayerID = sortingLayerId;
        right.localPosition = new Vector3(halfOffset, 0f, 0f);
        right.localScale = Vector3.one;
    }

    Sprite CreateHalfSprite(Sprite source, bool leftHalf)
    {
        if (source == null || source.texture == null) return null;

        int key = source.GetInstanceID() ^ (leftHalf ? 0x1 : 0x2);
        var cache = leftHalf ? alloyHalfLeftCache : alloyHalfRightCache;
        if (cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Rect rect = source.textureRect;
        float halfWidth = rect.width * 0.5f;
        Rect subRect = new Rect(rect.x + (leftHalf ? 0f : halfWidth), rect.y, halfWidth, rect.height);
        Vector2 pivot = new Vector2(subRect.width * 0.5f, subRect.height * 0.5f);
        Sprite halfSprite = Sprite.Create(source.texture, subRect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
        halfSprite.name = source.name + (leftHalf ? "_AlloyLeft" : "_AlloyRight");

        cache[key] = halfSprite;
        return halfSprite;
    }

    public void UpdateDirectionArrow(ref GameObject arrow, Transform parent, Dir dir)
    {
        if (parent == null) return;

        if (directionArrowPrefab != null)
        {
            if (arrow == null)
                arrow = Instantiate(directionArrowPrefab, parent.position, Quaternion.identity, parent);
            arrow.transform.SetParent(parent);
        }
        else
        {
            if (arrow == null)
            {
                arrow = new GameObject("DirectionArrow");
                arrow.transform.SetParent(parent);
            }

            arrow.transform.SetParent(parent);
            var lineRenderer = arrow.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = arrow.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = false;
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startWidth = 0.05f;
                lineRenderer.endWidth = 0.05f;
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.red;
                lineRenderer.sortingOrder = 20;
                lineRenderer.positionCount = 4;
                lineRenderer.textureMode = LineTextureMode.DistributePerSegment;
            }

            lineRenderer.SetPosition(0, new Vector3(0f, 0.18f, 0f));
            lineRenderer.SetPosition(1, new Vector3(0.12f, -0.02f, 0f));
            lineRenderer.SetPosition(2, new Vector3(0f, 0.06f, 0f));
            lineRenderer.SetPosition(3, new Vector3(-0.12f, -0.02f, 0f));
        }

        arrow.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        arrow.transform.localRotation = Quaternion.Euler(0, 0, DirToAngle(dir));
        arrow.name = "DirectionArrow";
    }

    public void AddMoney(float amount)
    {
        totalMoney += amount;
    }

    void DrawRecipeButton(Rect rect, string label, bool active, Action onClick)
    {
        Color prev = GUI.color;
        if (active) GUI.color = new Color(0.7f, 1f, 0.7f, 1f);
        if (GUI.Button(rect, label)) onClick();
        GUI.color = prev;
    }

    void DrawSelectedBuildingUI()
    {
        selectedUIRect = null;

        if (selected == null || Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(selected.Center() + new Vector3(0f, 0.6f, 0f));
        float boxWidth = 170f;
        float boxHeight = (selected is Forge) ? 90f : (selected is CraftingTable ? 80f : 0f);
        if (boxHeight <= 0f) return;

        float x = Mathf.Clamp(screenPos.x - boxWidth / 2f, 5f, Screen.width - boxWidth - 5f);
        float y = Mathf.Clamp(Screen.height - screenPos.y - boxHeight, 5f, Screen.height - boxHeight - 5f);

        Rect boxRect = new Rect(x, y, boxWidth, boxHeight);
        selectedUIRect = boxRect;
        string title = selected is Forge ? "Forge" : "Crafting Table";
        GUI.Box(boxRect, title);

        if (selected is Forge forge)
        {
            DrawRecipeButton(new Rect(boxRect.x + 10, boxRect.y + 20, boxWidth - 20, 20), "Ingot -> Plate", forge.recipe == Forge.ForgeRecipe.IngotToPlate, () => forge.recipe = Forge.ForgeRecipe.IngotToPlate);
            DrawRecipeButton(new Rect(boxRect.x + 10, boxRect.y + 45, boxWidth - 20, 20), "Ingot -> Bolt", forge.recipe == Forge.ForgeRecipe.IngotToBolt, () => forge.recipe = Forge.ForgeRecipe.IngotToBolt);
        }
        else if (selected is CraftingTable table)
        {
            var counts = table.InputCounts();
            GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 20, boxWidth - 20, 20), "2 Bolts + 1 Plate");
            GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 40, boxWidth - 20, 20), "= Reinforced Plate");
            GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 60, boxWidth - 20, 20), $"Inputs: {counts.bolts} bolts, {counts.plates} plates");
        }
    }

    bool IsMouseOverSelectedUI()
    {
        if (selectedUIRect == null) return false;

        Vector2 mouseGuiPos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        return selectedUIRect.Value.Contains(mouseGuiPos);
    }


    static Dir AngleToDir(float z)
    {
        int a = Mathf.RoundToInt(z) % 360;
        if (a < 0) a += 360;
        // 0 Sprite nach oben, 270 nach rechts
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
        GUIStyle moneyStyle = new GUIStyle(GUI.skin.label);
        moneyStyle.normal.textColor = Color.green;
        moneyStyle.alignment = TextAnchor.MiddleRight;
        moneyStyle.fontSize = 18;
        GUI.Label(new Rect(Screen.width - 180, 10, 170, 30), "€ " + totalMoney.ToString("F0"), moneyStyle);

        DrawSelectedBuildingUI();

        if (!ToggleBuildMenu) return;

        GUI.Box(new Rect(10, 10, 260, 280), "Build");
        if (GUI.Button(new Rect(20, 40, 90, 20), "Conveyor"))
        {
            placingConveyor = true;
            placingFurnace = false;
            placingForge = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            placingSeller = false;
            placingAlloyFurnace = false;
            placingCraftingTable = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(120, 40, 90, 20), "Furnace"))
        {
            placingFurnace = true;
            placingConveyor = false;
            placingForge = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            placingSeller = false;
            placingAlloyFurnace = false;
            placingCraftingTable = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(20, 70, 90, 20), "Forge"))
        {
            placingForge = true;
            placingFurnace = false;
            placingConveyor = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            placingSeller = false;
            placingAlloyFurnace = false;
            placingCraftingTable = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(120, 70, 90, 20), "Splitter 3"))
        {
            placingSplitter = true;
            placingConveyor = false;
            placingFurnace = false;
            placingForge = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            placingSeller = false;
            placingAlloyFurnace = false;
            placingCraftingTable = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(20, 100, 90, 20), "Miner"))
        {
            placingMiner = true;
            placingConveyor = false;
            placingFurnace = false;
            placingForge = false;
            placingSplitter = false;
            placingItemOnConveyor = false;
            placingSeller = false;
            placingCraftingTable = false;
            placingItemOnConveyor = false;
            placingAlloyFurnace = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(120, 100, 90, 20), "Seller"))
        {
            placingSeller = true;
            placingConveyor = false;
            placingFurnace = false;
            placingForge = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            placingCraftingTable = false;
            placingAlloyFurnace = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(20, 130, 90, 20), "Crafting Tbl"))
        {
            placingCraftingTable = true;
            placingSeller = false;
            placingConveyor = false;
            placingFurnace = false;
            placingForge = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            placingAlloyFurnace = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }
        if (GUI.Button(new Rect(120, 130, 90, 20), "Alloy Furn"))
        {
            placingAlloyFurnace = true;
            placingCraftingTable = false;
            placingSeller = false;
            placingConveyor = false;
            placingFurnace = false;
            placingForge = false;
            placingSplitter = false;
            placingMiner = false;
            placingItemOnConveyor = false;
            ResetPlacementDirection();
            ClearGhost();
            ClearSelection();
        }

        if (GUI.Button(new Rect(20, 160, 90, 20), "Debug Item"))
        {
            placingItemOnConveyor = true;
            placingConveyor = false;
            placingFurnace = false;
            placingForge = false;
            placingSplitter = false;
            placingMiner = false;
            placingSeller = false;
            placingCraftingTable = false;
            placingAlloyFurnace = false;
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

        GUI.Label(new Rect(20, 190, 220, 20), "Klick=Auswahl, R=Rotieren, X=Löschen");
        GUI.Label(new Rect(20, 210, 220, 20), "Drag=Bewegen, Pfeil=Ausgaberichtung");
        GUI.Label(new Rect(20, 230, 220, 20), "Splitter: L/F/R Round-Robin");
        GUI.Label(new Rect(20, 250, 220, 20), "Miner/Forge nur auf Nodes/Ingot");
    }
}