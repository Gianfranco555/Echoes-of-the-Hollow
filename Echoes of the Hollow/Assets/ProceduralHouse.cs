using UnityEngine;

public class ProceduralHouse : MonoBehaviour
{
    public int width = 6;
    public int depth = 8;
    public int height = 3;
    public float wallThickness = 0.2f;

    private void Awake()
    {
        float halfWidth = width / 2f;
        float halfDepth = depth / 2f;
        float halfThickness = wallThickness / 2f;

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.parent = transform;
        floor.transform.localScale = new Vector3(width, wallThickness, depth);
        floor.transform.localPosition = new Vector3(0f, -halfThickness, 0f);
        floor.AddComponent<BoxCollider>();

        // Roof
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.transform.parent = transform;
        roof.transform.localScale = new Vector3(width, wallThickness, depth);
        roof.transform.localPosition = new Vector3(0f, height + halfThickness, 0f);
        roof.AddComponent<BoxCollider>();

        // Walls
        const float doorWidth = 2f;
        const float doorHeight = 4f;
        float halfDoor = doorWidth / 2f;
        float sideWidth = (width - doorWidth) / 2f;

        // Front wall - pieces leaving doorway of fixed size
        GameObject frontLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontLeft.transform.parent = transform;
        frontLeft.transform.localScale = new Vector3(sideWidth, height, wallThickness);
        frontLeft.transform.localPosition = new Vector3(-halfDoor - sideWidth / 2f, height / 2f, -halfDepth + halfThickness);
        frontLeft.AddComponent<BoxCollider>();

        GameObject frontRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontRight.transform.parent = transform;
        frontRight.transform.localScale = new Vector3(sideWidth, height, wallThickness);
        frontRight.transform.localPosition = new Vector3(halfDoor + sideWidth / 2f, height / 2f, -halfDepth + halfThickness);
        frontRight.AddComponent<BoxCollider>();

        // Header above the doorway if the house is taller than the door
        float headerHeight = height - doorHeight;
        if (headerHeight > 0f)
        {
            GameObject frontTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontTop.transform.parent = transform;
            frontTop.transform.localScale = new Vector3(doorWidth, headerHeight, wallThickness);
            frontTop.transform.localPosition = new Vector3(0f, doorHeight + headerHeight / 2f, -halfDepth + halfThickness);
            frontTop.AddComponent<BoxCollider>();
        }

        // Back wall
        GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.transform.parent = transform;
        back.transform.localScale = new Vector3(width, height, wallThickness);
        back.transform.localPosition = new Vector3(0f, height / 2f, halfDepth - halfThickness);
        back.AddComponent<BoxCollider>();

        // Left wall
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.transform.parent = transform;
        left.transform.localScale = new Vector3(wallThickness, height, depth);
        left.transform.localPosition = new Vector3(-halfWidth + halfThickness, height / 2f, 0f);
        left.AddComponent<BoxCollider>();

        // Right wall
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        right.transform.parent = transform;
        right.transform.localScale = new Vector3(wallThickness, height, depth);
        right.transform.localPosition = new Vector3(halfWidth - halfThickness, height / 2f, 0f);
        right.AddComponent<BoxCollider>();
    }
}