/*
1. Use metric units based on the FT constant for all measurements.
2. Build floors, walls and roof using primitive cubes for simplicity.
3. Maintain consistent pivot orientation for modular placement.
4. Reuse materials to keep draw calls low.
5. Encapsulate key dimensions as static readonly fields.
6. Provide helper methods for creating materials and geometry.
7. Organize the hierarchy House -> MainFloor | Basement | Attic.
8. Include an editor rebuild menu option for convenience.
*/
using UnityEngine;

public enum Swing { InLeft, InRight, OutLeft, OutRight, SlideLeft, SlideRight }

public class AdvancedHouseBuilder : MonoBehaviour
{
    public const float FT = 0.3048f;

    static readonly float WALL_THICKNESS = 0.5f * FT;
    static readonly float DOOR_WIDTH = 3f * FT;
    static readonly float DOOR_HEIGHT = 7f * FT;
    static readonly float FLOOR_HEIGHT = 8f * FT;
    public const float WIDE_CASED_OPENING_WIDTH = 5f * FT;

    Material wallMat;
    Material floorMat;
    Material roofMat;

    static Material MakeMat(Color c)
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.color = c;
        return mat;
    }

    Transform house;
    Transform mainFloor;
    Transform basement;
    Transform attic;

    struct Cursor { public float x, z; }
    Cursor cursor;

    void Awake()
    {
        wallMat = MakeMat(Color.gray);
        floorMat = MakeMat(Color.white);
        roofMat = MakeMat(Color.red);
    }

    void Start()
    {
        house = new GameObject("House").transform;
        house.SetParent(transform, false);

        mainFloor = new GameObject("MainFloor").transform;
        mainFloor.SetParent(house, false);

        basement = new GameObject("Basement").transform;
        basement.SetParent(house, false);

        attic = new GameObject("Attic").transform;
        attic.SetParent(house, false);

        BuildGarage();
        BuildFoyer();
        BuildLivingRoom();
    }

    void BuildGarage()
    {
        const float INTERIOR_W = 11.33f * FT;
        const float INTERIOR_D = 20f * FT;

        float exteriorW = INTERIOR_W + WALL_THICKNESS * 2f;
        float exteriorD = INTERIOR_D + WALL_THICKNESS * 2f;

        var garage = new GameObject("Garage").transform;
        garage.SetParent(mainFloor, false);

        Vector3 baseCorner = new Vector3(cursor.x, 0f, cursor.z);

        // Floor slab
        CreateCube(
            "Floor",
            baseCorner + new Vector3(exteriorW * 0.5f, WALL_THICKNESS * 0.5f, exteriorD * 0.5f),
            new Vector3(exteriorW, WALL_THICKNESS, exteriorD),
            floorMat,
            garage);

        // Walls
        BuildSolidWall(
            "Front",
            baseCorner + new Vector3(0f, 0f, WALL_THICKNESS * 0.5f),
            exteriorW,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            true,
            garage);
        BuildSolidWall(
            "Back",
            baseCorner + new Vector3(0f, 0f, exteriorD - WALL_THICKNESS * 0.5f),
            exteriorW,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            true,
            garage);
        BuildSolidWall(
            "Left",
            baseCorner + new Vector3(WALL_THICKNESS * 0.5f, 0f, 0f),
            exteriorD,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            false,
            garage);
        BuildSolidWall(
            "Right",
            baseCorner + new Vector3(exteriorW - WALL_THICKNESS * 0.5f, 0f, 0f),
            exteriorD,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            false,
            garage);

        // Advance cursor beyond the garage (far exterior edge)
        cursor.x += exteriorW;
    }

    void BuildLivingRoom()
    {
        const float W = 12.67f * FT;
        const float D = 15f * FT;

        float x0 = cursor.x;
        float z0 = cursor.z;

        // Assuming the 'left' wall is shared or connects to an adjacent room's opening (e.g., Foyer's right wall),
        // so the exterior width calculation only accounts for the new 'right' exterior wall.
        float exteriorW = W + WALL_THICKNESS;
        float exteriorD = D + WALL_THICKNESS * 2f;

        var living = new GameObject("LivingRoom").transform;
        living.SetParent(mainFloor, false);

        Vector3 baseCorner = new Vector3(x0, 0f, z0);

        // Floor slab
        CreateCube(
            "Floor",
            baseCorner + new Vector3(exteriorW * 0.5f, WALL_THICKNESS * 0.5f, exteriorD * 0.5f),
            new Vector3(exteriorW, WALL_THICKNESS, exteriorD),
            floorMat,
            living);

        // Walls
        var front = BuildWallWithOpening(
            "Front",
            baseCorner + new Vector3(0f, 0f, WALL_THICKNESS * 0.5f),
            exteriorW,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            true,
            true,
            exteriorW * 0.5f,
            8f * FT,
            5f * FT,
            living,
            true);
        BuildWindow(
            "FrontWindow",
            new Vector3(exteriorW * 0.5f, 2.5f * FT, 0f),
            8f * FT,
            5f * FT,
            WALL_THICKNESS,
            front);

        BuildSolidWall(
            "Back",
            baseCorner + new Vector3(0f, 0f, exteriorD - WALL_THICKNESS * 0.5f),
            exteriorW,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            true,
            living);

        BuildSolidWall(
            "Right",
            baseCorner + new Vector3(exteriorW - WALL_THICKNESS * 0.5f, 0f, 0f),
            exteriorD,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            false,
            living);

        // Advance cursor by interior width plus the far wall thickness
        cursor.x += exteriorW;
    }

    void BuildFoyer()
    {
        const float W = 6f * FT;
        const float D = 8f * FT;

        float x0 = cursor.x;
        float z0 = cursor.z;

        float exteriorW = W + WALL_THICKNESS * 2f;
        float exteriorD = D + WALL_THICKNESS * 2f;

        var foyer = new GameObject("Foyer").transform;
        foyer.SetParent(mainFloor, false);

        Vector3 baseCorner = new Vector3(x0, 0f, z0);

        // Floor slab
        CreateCube(
            "Floor",
            baseCorner + new Vector3(exteriorW * 0.5f, WALL_THICKNESS * 0.5f, exteriorD * 0.5f),
            new Vector3(exteriorW, WALL_THICKNESS, exteriorD),
            floorMat,
            foyer);

        // Walls
        var front = BuildWallWithOpening(
            "Front",
            baseCorner + new Vector3(0f, 0f, WALL_THICKNESS * 0.5f),
            exteriorW,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            true,
            true,
            exteriorW * 0.5f,
            DOOR_WIDTH,
            DOOR_HEIGHT,
            foyer);
        BuildDoor(
            "EntryDoor",
            new Vector3(exteriorW * 0.5f, DOOR_HEIGHT * 0.5f, 0f),
            DOOR_WIDTH,
            DOOR_HEIGHT,
            WALL_THICKNESS,
            Swing.InLeft,
            front);

        BuildSolidWall(
            "Back",
            baseCorner + new Vector3(0f, 0f, exteriorD - WALL_THICKNESS * 0.5f),
            exteriorW,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            true,
            foyer);

        BuildSolidWall(
            "Left",
            baseCorner + new Vector3(WALL_THICKNESS * 0.5f, 0f, 0f),
            exteriorD,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            false,
            foyer);

        BuildWallWithOpening(
            "Right",
            baseCorner + new Vector3(exteriorW - WALL_THICKNESS * 0.5f, 0f, 0f),
            exteriorD,
            FLOOR_HEIGHT,
            WALL_THICKNESS,
            false,
            true,
            exteriorD * 0.5f,
            WIDE_CASED_OPENING_WIDTH,
            DOOR_HEIGHT,
            foyer);

        cursor.x += exteriorW;
    }

    GameObject CreateCube(string name, Vector3 centre, Vector3 size, Material m, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = centre;
        go.transform.localScale = size;
        if (m != null)
            go.GetComponent<Renderer>().material = m;
        return go;
    }

    GameObject BuildSolidWall(string name, Vector3 start, float length, float height, float thick, bool alongX, Transform parent)
    {
        Vector3 size;
        Vector3 centre;
        if (alongX)
        {
            size = new Vector3(length, height, thick);
            centre = start + new Vector3(length * 0.5f, height * 0.5f, 0f);
        }
        else
        {
            size = new Vector3(thick, height, length);
            centre = start + new Vector3(0f, height * 0.5f, length * 0.5f);
        }
        return CreateCube(name, centre, size, wallMat, parent);
    }

    GameObject BuildDoor(string name, Vector3 centre, float width, float height, float thickness, Swing swing, Transform parent)
    {
        // Distance from door centre to hinge pivot along the door width axis.
        float hingeOffset = width * 0.5f - thickness * 0.5f;

        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);

        Vector3 hinge = Vector3.zero;
        switch (swing)
        {
            case Swing.InLeft:
            case Swing.OutLeft:
                hinge.x = -hingeOffset;
                break;
            case Swing.InRight:
            case Swing.OutRight:
                hinge.x = hingeOffset;
                break;
            case Swing.SlideLeft:
            case Swing.SlideRight:
                // Sliding doors aren't pivoted like regular doors. Warn until
                // proper sliding mechanics are implemented.
                Debug.LogWarning($"Sliding door type {swing} for door '{name}' is not implemented.");
                break;
        }

        // Place the pivot at the hinge and offset the panel so that it sits
        // flush in the closed position.
        pivot.localPosition = centre + hinge;
        var panel = CreateCube("Panel", -hinge, new Vector3(width, height, thickness), wallMat, pivot);
        return pivot.gameObject;
    }

    GameObject BuildWindow(string name, Vector3 centre, float w, float h, float frameT, Transform parent)
    {
        var window = new GameObject(name).transform;
        window.SetParent(parent, false);
        window.localPosition = centre;

        float halfW = w * 0.5f;
        float halfH = h * 0.5f;
        float t = frameT;

        CreateCube("Left", new Vector3(-halfW + t * 0.5f, 0f, 0f), new Vector3(t, h, t), wallMat, window);
        CreateCube("Right", new Vector3(halfW - t * 0.5f, 0f, 0f), new Vector3(t, h, t), wallMat, window);
        CreateCube("Top", new Vector3(0f, halfH - t * 0.5f, 0f), new Vector3(w, t, t), wallMat, window);
        CreateCube("Bottom", new Vector3(0f, -halfH + t * 0.5f, 0f), new Vector3(w, t, t), wallMat, window);

        return window.gameObject;
    }

    Transform BuildWallWithOpening(string name, Vector3 start, float length, float height, float thick, bool alongX, bool opening, float openCenter, float openWidth, float openHeight, Transform parent, bool isWindow = false)
    {
        var grp = new GameObject(name).transform;
        grp.SetParent(parent, false);
        grp.localPosition = start;

        void SpawnSeg(float x0, float y0, float z0, float lenX, float lenY, float lenZ)
        {
            Vector3 size, centre;
            if (alongX)
            {
                size = new Vector3(lenX, lenY, lenZ);
                centre = new Vector3(x0 + lenX * 0.5f, y0 + lenY * 0.5f, z0);
            }
            else
            {
                size = new Vector3(lenZ, lenY, lenX);
                centre = new Vector3(z0, y0 + lenY * 0.5f, x0 + lenX * 0.5f);
            }
            CreateCube("Seg", centre, size, wallMat, grp);
        }

        if (!opening)
        {
            SpawnSeg(0f, 0f, 0f, length, height, thick);
            return grp;
        }

        float halfOpen = openWidth * 0.5f;
        float leftLen = openCenter - halfOpen;
        float rightLen = length - (openCenter + halfOpen);
        float aboveHeight = height - openHeight;

        if (leftLen > 0f)
        {
            SpawnSeg(0f, 0f, 0f, leftLen, height, thick);
        }

        if (rightLen > 0f)
        {
            SpawnSeg(openCenter + halfOpen, 0f, 0f, rightLen, height, thick);
        }

        if (isWindow)
        {
            // Leave the opening empty for windows. The window frame
            // will be created separately by BuildWindow().
        }

        if (aboveHeight > 0f)
        {
            SpawnSeg(leftLen, openHeight, 0f, openWidth, aboveHeight, thick);
        }

        return grp;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("House/Rebuild")]
    static void Rebuild() { UnityEditor.SceneView.lastActiveSceneView.FrameSelected(); }
#endif
}
