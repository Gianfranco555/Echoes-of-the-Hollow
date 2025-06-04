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
                    ProcessWallCutouts(segment, wall.name, housePlan);
                    wallIndex++;
                }
            }
        }

        // Build the stairwell enclosure if the plan defines one
        RoomData? stairwell = null;
        foreach (RoomData room in housePlan.rooms)
        {
            if (room.roomId == "StairwellEnclosure")
            {
                stairwell = room;
                break;
            }
        }

        if (stairwell.HasValue && stairwell.Value.dimensions.x > 0f && stairwell.Value.dimensions.y > 0f)
        {
            BuildRectangularEnclosure(stairwell.Value, root.transform, storyHeight, housePlan.interiorWallThickness, builtKeys, ref wallIndex);
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

    private static void BuildRectangularEnclosure(RoomData stairwell,
                                                  Transform parent,
                                                  float height,
                                                  float thickness,
                                                  HashSet<string> builtKeys,
                                                  ref int wallIndex)
    {
        Vector3 sw = stairwell.position;
        Vector3 se = stairwell.position + new Vector3(stairwell.dimensions.x, 0f, 0f);
        Vector3 ne = stairwell.position + new Vector3(stairwell.dimensions.x, 0f, stairwell.dimensions.y);
        Vector3 nw = stairwell.position + new Vector3(0f, 0f, stairwell.dimensions.y);

        Vector3[] corners = new[] { sw, se, ne, nw };
        for (int i = 0; i < 4; i++)
        {
            Vector3 start = corners[i];
            Vector3 end = corners[(i + 1) % 4];
            string key = GetSegmentKey(start, end);
            if (!builtKeys.Add(key))
            {
                continue;
            }

            GameObject wall = BuildWallSegment(start, end, height, thickness);
            if (wall != null)
            {
                wall.name = $"Wall_{stairwell.roomId}_{wallIndex}";
                wall.transform.SetParent(parent, false);
                wallIndex++;
            }
        }
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

    private static void ProcessWallCutouts(WallSegment segment, string wallId, HousePlanSO plan)
    {
        if (plan == null)
        {
            return;
        }

        if (segment.doorIdsOnWall != null)
        {
            foreach (string id in segment.doorIdsOnWall)
            {
                foreach (DoorSpec spec in plan.doors)
                {
                    if (spec.doorId == id)
                    {
                        Log.Info($"Placeholder for cut-out: {spec.doorId} on wall {wallId} at position {spec.position} with size {spec.width}x{spec.height}");
                        break;
                    }
                }
            }
        }

        if (segment.windowIdsOnWall != null)
        {
            foreach (string id in segment.windowIdsOnWall)
            {
                foreach (WindowSpec spec in plan.windows)
                {
                    if (spec.windowId == id)
                    {
                        Log.Info($"Placeholder for cut-out: {spec.windowId} on wall {wallId} at position {spec.position} with size {spec.width}x{spec.height}");
                        break;
                    }
                }
            }
        }

        if (segment.openingIdsOnWall != null)
        {
            foreach (string id in segment.openingIdsOnWall)
            {
                foreach (OpeningSpec spec in plan.openings)
                {
                    if (spec.openingId == id)
                    {
                        Log.Info($"Placeholder for cut-out: {spec.openingId} on wall {wallId} at position {spec.position} with size {spec.width}x{spec.height}");
                        break;
                    }
                }
            }
        }
    }
}
