using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides utilities for building room related geometry such as interior walls.
/// </summary>
public static class RoomBuilder
{
    /// <summary>
    /// Generates mesh geometry for all interior wall segments defined in the
    /// supplied <see cref="HousePlanSO"/>. Returned GameObject is the parent
    /// of all generated wall meshes.
    /// </summary>
    /// <param name="housePlan">Plan containing rooms and wall data.</param>
    /// <param name="storyHeight">Height to use for the generated walls.</param>
    /// <returns>Parent GameObject for all interior wall meshes.</returns>
    public static GameObject GenerateInteriorWalls(HousePlanSO housePlan, float storyHeight)
    {
        if (housePlan == null)
        {
            Debug.LogError("HousePlanSO is null. Cannot build interior walls.");
            return null;
        }

        GameObject root = new GameObject("Walls_Interior");
        HashSet<string> builtKeys = new HashSet<string>();
        int wallIndex = 0;

        foreach (RoomData room in housePlan.rooms)
        {
            if (room.walls == null)
            {
                continue;
            }

            foreach (WallSegment segment in room.walls)
            {
                if (segment.isExterior)
                {
                    continue;
                }

                Vector3 startWorld = room.position + segment.startPoint;
                Vector3 endWorld = room.position + segment.endPoint;
                string key = GetSegmentKey(startWorld, endWorld);
                if (!builtKeys.Add(key))
                {
                    continue; // Avoid duplicate walls shared between rooms
                }

                GameObject wall = BuildWallSegment(startWorld, endWorld, storyHeight, housePlan.interiorWallThickness);
                if (wall != null)
                {
                    wall.name = $"Wall_{room.roomId}_{wallIndex}";
                    wall.transform.SetParent(root.transform, false);
                    wallIndex++;
                }
            }
        }

        return root;
    }

    // ---------------------------------------------------------------------
    private static GameObject BuildWallSegment(Vector3 startWorld, Vector3 endWorld, float height, float thickness)
    {
        Vector3 direction = endWorld - startWorld;
        float length = direction.magnitude;
        if (length <= 0.001f)
        {
            return null;
        }

        float halfThickness = thickness * 0.5f;

        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(0f, 0f, -halfThickness),
            new Vector3(length, 0f, -halfThickness),
            new Vector3(0f, height, -halfThickness),
            new Vector3(length, height, -halfThickness),
            new Vector3(0f, 0f, halfThickness),
            new Vector3(length, 0f, halfThickness),
            new Vector3(0f, height, halfThickness),
            new Vector3(length, height, halfThickness)
        };

        int[] triangles = new int[36]
        {
            0, 2, 1, 1, 2, 3,
            4, 5, 6, 5, 7, 6,
            0, 1, 4, 1, 5, 4,
            2, 6, 3, 3, 6, 7,
            1, 3, 5, 3, 7, 5,
            0, 4, 2, 2, 4, 6
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

        GameObject wall = new GameObject("InteriorWall");
        MeshFilter filter = wall.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        wall.AddComponent<MeshRenderer>();

        wall.transform.position = startWorld;
        wall.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        return wall;
    }

    private static string GetSegmentKey(Vector3 start, Vector3 end)
    {
        Vector3 a = start;
        Vector3 b = end;
        if (a.x > b.x || (Mathf.Approximately(a.x, b.x) && a.z > b.z))
        {
            Vector3 temp = a;
            a = b;
            b = temp;
        }

        string aStr = $"{a.x:F3}_{a.y:F3}_{a.z:F3}";
        string bStr = $"{b.x:F3}_{b.y:F3}_{b.z:F3}";
        return $"{aStr}-{bStr}";
    }
}
