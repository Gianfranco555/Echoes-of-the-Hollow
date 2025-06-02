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

    GameObject BuildDoor(string name, Vector3 centre, float w, float h, float t, Swing swing, Transform parent)
    {
        float halfW = w * 0.5f;
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        Vector3 hingeOffset = Vector3.zero;
        switch (swing)
        {
            case Swing.InLeft:
            case Swing.OutLeft:
                hingeOffset.x = -halfW;
                break;
            case Swing.InRight:
            case Swing.OutRight:
                hingeOffset.x = halfW;
                break;
        }
        pivot.localPosition = centre + hingeOffset;

        var panel = CreateCube("Panel", -hingeOffset + new Vector3(0f, 0f, 0f), new Vector3(w, h, t), wallMat, pivot);
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

    void BuildWallWithOpening(string name, Vector3 start, float length, float height, float thick, bool alongX, bool opening, float openCenter, float openWidth, float openHeight, Transform parent)
    {
        if (!opening)
        {
            BuildSolidWall(name, start, length, height, thick, alongX, parent);
            return;
        }

        float halfOpen = openWidth * 0.5f;
        float leftLen = openCenter - halfOpen;
        float rightLen = length - (openCenter + halfOpen);
        float aboveHeight = height - openHeight;

        if (leftLen > 0f)
        {
            BuildSolidWall(name + "_L", start, leftLen, height, thick, alongX, parent);
        }

        if (rightLen > 0f)
        {
            Vector3 rs = alongX ? start + new Vector3(openCenter + halfOpen, 0f, 0f)
                                : start + new Vector3(0f, 0f, openCenter + halfOpen);
            BuildSolidWall(name + "_R", rs, rightLen, height, thick, alongX, parent);
        }

        if (aboveHeight > 0f)
        {
            Vector3 ts = alongX ? start + new Vector3(leftLen, openHeight, 0f)
                                : start + new Vector3(0f, openHeight, leftLen);
            BuildSolidWall(name + "_T", ts, openWidth, aboveHeight, thick, alongX, parent);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("House/Rebuild")]
    static void Rebuild() { UnityEditor.SceneView.lastActiveSceneView.FrameSelected(); }
#endif
}
