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

#if UNITY_EDITOR
    [UnityEditor.MenuItem("House/Rebuild")]
    static void Rebuild() { UnityEditor.SceneView.lastActiveSceneView.FrameSelected(); }
#endif
}
