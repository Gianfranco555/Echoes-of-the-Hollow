using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates wall meshes based on <see cref="HousePlanSO"/> data.
/// Currently supports only solid exterior walls.
/// </summary>
public static class WallBuilder
{
    /// <summary>
    /// Builds mesh geometry for all exterior walls in the supplied <see cref="HousePlanSO"/>.
    /// </summary>
    /// <param name="housePlan">House plan describing rooms and wall segments.</param>
    /// <param name="storyHeight">Height of the walls to generate.</param>
    /// <returns>Root GameObject containing generated exterior wall meshes.</returns>
    public static GameObject GenerateExteriorWalls(HousePlanSO housePlan, float storyHeight)
    {
        if (housePlan == null)
        {
            Debug.LogError("HousePlanSO is null. Cannot build walls.");
            return null;
        }

        GameObject root = new GameObject("Walls_Exterior");
        foreach (RoomData room in housePlan.rooms)
        {
            if (room.walls == null)
            {
                continue;
            }

            int wallIndex = 0;
            foreach (WallSegment segment in room.walls)
            {
                if (!segment.isExterior)
                {
                    wallIndex++;
                    continue;
                }

                GameObject wallObj = BuildWallSegment(segment, room.position, storyHeight, housePlan.exteriorWallThickness);
                if (wallObj != null)
                {
                    wallObj.name = $"Wall_{room.roomId}_{wallIndex}";
                    wallObj.transform.SetParent(root.transform, false);
                }
                wallIndex++;
            }
        }

        return root;
    }

    // ---------------------------------------------------------------------
    private static GameObject BuildWallSegment(WallSegment segment, Vector3 roomOffset, float height, float thickness)
    {
        Vector3 startWorld = roomOffset + segment.startPoint;
        Vector3 endWorld = roomOffset + segment.endPoint;
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
            // Front
            0, 2, 1, 1, 2, 3,
            // Back
            4, 5, 6, 5, 7, 6,
            // Bottom
            0, 1, 4, 1, 5, 4,
            // Top
            2, 6, 3, 3, 6, 7,
            // Right
            1, 3, 5, 3, 7, 5,
            // Left
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

        GameObject wall = new GameObject("ExteriorWall");
        MeshFilter filter = wall.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        wall.AddComponent<MeshRenderer>();

        wall.transform.position = startWorld;
        wall.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);
        return wall;
    }
}

