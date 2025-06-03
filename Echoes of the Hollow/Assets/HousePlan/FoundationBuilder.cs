using UnityEngine;

/// <summary>
/// Utility for procedurally generating a flat concrete slab foundation mesh
/// based on <see cref="HousePlanSO"/> data.
/// </summary>
public static class FoundationBuilder
{
    /// <summary>
    /// Generates a concrete foundation GameObject for the supplied house plan.
    /// The foundation spans the overall main level footprint including
    /// garage, covered patio and covered entry.
    /// </summary>
    /// <param name="housePlan">Plan describing the house layout.</param>
    /// <returns>GameObject containing the generated foundation mesh.</returns>
    public static GameObject GenerateFoundation(HousePlanSO housePlan)
    {
        if (housePlan == null)
        {
            Debug.LogError("HousePlanSO is null. Cannot build foundation.");
            return null;
        }

        Bounds bounds = housePlan.CalculateBounds();
        float width = bounds.size.x;
        float depth = bounds.size.z;
        const float THICKNESS = 0.15f; // meters

        // Vertices for a simple rectangular slab aligned to origin.
        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(0f, 0f, 0f),              // 0 - bottom SW
            new Vector3(width, 0f, 0f),           // 1 - bottom SE
            new Vector3(0f, 0f, depth),           // 2 - bottom NW
            new Vector3(width, 0f, depth),        // 3 - bottom NE
            new Vector3(0f, THICKNESS, 0f),       // 4 - top SW
            new Vector3(width, THICKNESS, 0f),    // 5 - top SE
            new Vector3(0f, THICKNESS, depth),    // 6 - top NW
            new Vector3(width, THICKNESS, depth)  // 7 - top NE
        };

        int[] triangles = new int[36]
        {
            // Bottom
            0, 2, 1, 1, 2, 3,
            // Top
            4, 5, 6, 5, 7, 6,
            // Sides
            0, 1, 4, 1, 5, 4,
            1, 3, 5, 3, 7, 5,
            3, 2, 7, 2, 6, 7,
            2, 0, 6, 0, 4, 6
        };
        Vector2[] uvs = new Vector2[8]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            uv = uvs
        };
        mesh.RecalculateNormals();

        GameObject foundation = new GameObject("Foundation");
        MeshFilter filter = foundation.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        foundation.AddComponent<MeshRenderer>();

        Vector3 offset = new Vector3(-bounds.min.x, 0f, -bounds.min.z);
        foundation.transform.position = offset;

        return foundation;
    }
}
