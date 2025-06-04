using System.Collections.Generic;
using System.Linq; // Added for Distinct() and OrderBy()
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

                // startWorld and endWorld are now calculated inside BuildWallSegment
                string key = GetSegmentKey(room.position + segment.startPoint, room.position + segment.endPoint);
                if (!builtKeys.Add(key))
                {
                    continue; // Avoid duplicate walls shared between rooms
                }

                GameObject wall = BuildWallSegment(segment, room.position, storyHeight, housePlan.interiorWallThickness, housePlan);
                if (wall != null)
                {
                    wall.name = $"Wall_{room.roomId}_{wallIndex}";
                    wall.transform.SetParent(root.transform, false);
                    // ProcessWallCutouts(segment, wall.name, housePlan); // Removed call
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
            BuildRectangularEnclosure(stairwell.Value, root.transform, storyHeight, housePlan.interiorWallThickness, builtKeys, ref wallIndex, housePlan); // Added housePlan
        }

        return root;
    }

    // ---------------------------------------------------------------------
    private static Mesh GenerateWallSliceMesh(float sliceLength, float sliceHeight, float thickness, Vector3 sliceOffset)
    {
        float halfThickness = thickness * 0.5f;

        Vector3[] vertices = new Vector3[8]
        {
            sliceOffset + new Vector3(0f, 0f, -halfThickness),
            sliceOffset + new Vector3(sliceLength, 0f, -halfThickness),
            sliceOffset + new Vector3(0f, sliceHeight, -halfThickness),
            sliceOffset + new Vector3(sliceLength, sliceHeight, -halfThickness),
            sliceOffset + new Vector3(0f, 0f, halfThickness),
            sliceOffset + new Vector3(sliceLength, 0f, halfThickness),
            sliceOffset + new Vector3(0f, sliceHeight, halfThickness),
            sliceOffset + new Vector3(sliceLength, sliceHeight, halfThickness)
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
        return mesh;
    }

    // ---------------------------------------------------------------------
    private static GameObject BuildWallSegment(WallSegment segment, Vector3 roomPosition, float storyHeight, float thickness, HousePlanSO housePlan)
    {
        // Corrected: Calculate wallStartWorld, wallEndWorld, and segmentLength first
        Vector3 wallStartWorld = roomPosition + segment.startPoint;
        Vector3 wallEndWorld = roomPosition + segment.endPoint;
        float segmentLength = (wallEndWorld - wallStartWorld).magnitude;

        if (segmentLength <= 0.01f) // Negligible length
        {
            return null;
        }

        // Define directions for rotation (original) and dot products (normalized)
        Vector3 originalDirection = wallEndWorld - wallStartWorld;
        Vector3 wallDirection = originalDirection.normalized; // Used for dot products

        GameObject wallRoot = new GameObject("WallSegment"); // Standardized name
        wallRoot.transform.position = wallStartWorld; // Use new variable
        wallRoot.transform.rotation = Quaternion.FromToRotation(Vector3.right, originalDirection); // Use non-normalized for rotation

        List<float> cutMarkers = new List<float> { 0f, segmentLength }; // Initialize before adding opening markers

        // Gather Openings
        List<DoorSpec> relevantDoors = new List<DoorSpec>();
        List<WindowSpec> relevantWindows = new List<WindowSpec>(); // Interior windows might be rare but supported
        List<OpeningSpec> relevantOpenings = new List<OpeningSpec>();

        if (housePlan != null) // Ensure housePlan is not null
        {
            if (segment.doorIdsOnWall != null && housePlan.doors != null)
            {
                foreach (string doorId in segment.doorIdsOnWall)
                {
                    DoorSpec door = housePlan.doors.Find(d => d.doorId == doorId);
                    if (door.doorId != null) relevantDoors.Add(door);
                }
            }

            if (segment.windowIdsOnWall != null && housePlan.windows != null)
            {
                foreach (string windowId in segment.windowIdsOnWall)
                {
                    WindowSpec window = housePlan.windows.Find(w => w.windowId == windowId);
                    if (window.windowId != null) relevantWindows.Add(window);
                }
            }

            if (segment.openingIdsOnWall != null && housePlan.openings != null)
            {
                foreach (string openingId in segment.openingIdsOnWall)
                {
                    OpeningSpec opening = housePlan.openings.Find(o => o.openingId == openingId);
                    if (opening.openingId != null) relevantOpenings.Add(opening);
                }
            }
        }

        // Add Cut Markers from Openings using corrected logic (wallDirection is already defined)
        foreach (DoorSpec door in relevantDoors)
        {
            Vector3 doorWorldPosition = roomPosition + door.position; // Assuming door.position is relative to roomPosition
            Vector3 vectorFromWallStartToDoor = doorWorldPosition - wallStartWorld;
            float distanceAlongWall = Vector3.Dot(vectorFromWallStartToDoor, wallDirection);
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall, 0f, segmentLength));
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall + door.width, 0f, segmentLength));
        }
        foreach (WindowSpec window in relevantWindows)
        {
            Vector3 windowWorldPosition = roomPosition + window.position; // Assuming window.position is relative to roomPosition
            Vector3 vectorFromWallStartToWindow = windowWorldPosition - wallStartWorld;
            float distanceAlongWall = Vector3.Dot(vectorFromWallStartToWindow, wallDirection);
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall, 0f, segmentLength));
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall + window.width, 0f, segmentLength));
        }
        foreach (OpeningSpec opening in relevantOpenings)
        {
            Vector3 openingWorldPosition = opening.position; // opening.position is relative to house origin
            Vector3 vectorFromWallStartToOpening = openingWorldPosition - wallStartWorld;
            float distanceAlongWall = Vector3.Dot(vectorFromWallStartToOpening, wallDirection);
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall, 0f, segmentLength));
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall + opening.width, 0f, segmentLength));
        }

        // Sort and remove duplicate markers
        cutMarkers = cutMarkers.Distinct().OrderBy(m => m).ToList();

        // Iterate Through Marker Pairs and build slices
        for (int i = 0; i < cutMarkers.Count - 1; i++)
        {
            float markerA = cutMarkers[i];
            float markerB = cutMarkers[i + 1];
            float sliceStart = markerA;
            float sliceLength = markerB - markerA;

            if (sliceLength <= 0.01f) // Negligible length, skip
            {
                continue;
            }

            float sliceMidPoint = sliceStart + sliceLength / 2f;
            bool openingFound = false;

            // Check for Windows
            foreach (WindowSpec window in relevantWindows)
            {
                if (sliceMidPoint >= window.position.x && sliceMidPoint < (window.position.x + window.width))
                {
                    // Sill Slice (below window)
                    if (window.sillHeight > 0.01f)
                    {
                        Mesh sillMesh = GenerateWallSliceMesh(sliceLength, window.sillHeight, thickness, Vector3.zero);
                        GameObject sillGo = new GameObject("Sill");
                        sillGo.AddComponent<MeshFilter>().mesh = sillMesh;
                        sillGo.AddComponent<MeshRenderer>(); // TODO: Assign material
                        sillGo.transform.SetParent(wallRoot.transform, false);
                        sillGo.transform.localPosition = new Vector3(sliceStart, 0, 0);
                    }

                    // Lintel Slice (above window)
                    float lintelStartY = window.sillHeight + window.height;
                    float lintelHeight = storyHeight - lintelStartY;
                    if (lintelHeight > 0.01f)
                    {
                        Mesh lintelMesh = GenerateWallSliceMesh(sliceLength, lintelHeight, thickness, Vector3.zero);
                        GameObject lintelGo = new GameObject("Lintel");
                        lintelGo.AddComponent<MeshFilter>().mesh = lintelMesh;
                        lintelGo.AddComponent<MeshRenderer>(); // TODO: Assign material
                        lintelGo.transform.SetParent(wallRoot.transform, false);
                        lintelGo.transform.localPosition = new Vector3(sliceStart, lintelStartY, 0);
                    }
                    openingFound = true;
                    break;
                }
            }
            if (openingFound) continue;

            // Check for Doors
            foreach (DoorSpec door in relevantDoors)
            {
                if (sliceMidPoint >= door.position.x && sliceMidPoint < (door.position.x + door.width))
                {
                    // Header Slice (above door)
                    float headerHeight = storyHeight - door.height;
                    if (headerHeight > 0.01f)
                    {
                        Mesh headerMesh = GenerateWallSliceMesh(sliceLength, headerHeight, thickness, Vector3.zero);
                        GameObject headerGo = new GameObject("Header");
                        headerGo.AddComponent<MeshFilter>().mesh = headerMesh;
                        headerGo.AddComponent<MeshRenderer>(); // TODO: Assign material
                        headerGo.transform.SetParent(wallRoot.transform, false);
                        headerGo.transform.localPosition = new Vector3(sliceStart, door.height, 0);
                    }
                    openingFound = true;
                    break;
                }
            }
            if (openingFound) continue;

            // Check for other Openings
            foreach (OpeningSpec opening in relevantOpenings)
            {
                if (sliceMidPoint >= opening.position.x && sliceMidPoint < (opening.position.x + opening.width))
                {
                    openingFound = true;
                    break;
                }
            }
            if (openingFound) continue;

            // Solid Wall Slice
            Mesh solidWallMesh = GenerateWallSliceMesh(sliceLength, storyHeight, thickness, Vector3.zero);
            GameObject solidWallGo = new GameObject("SolidSlice");
            solidWallGo.AddComponent<MeshFilter>().mesh = solidWallMesh;
            solidWallGo.AddComponent<MeshRenderer>(); // TODO: Assign material
            solidWallGo.transform.SetParent(wallRoot.transform, false);
            solidWallGo.transform.localPosition = new Vector3(sliceStart, 0, 0);
        }
        return wallRoot;
    }

    private static void BuildRectangularEnclosure(RoomData stairwell,
                                                  Transform parent,
                                                  float height,
                                                  float thickness,
                                                  HashSet<string> builtKeys,
                                                  ref int wallIndex,
                                                  HousePlanSO housePlan) // Added housePlan
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

            // Create a temporary WallSegment for BuildWallSegment
            WallSegment tempSegment = new WallSegment
            {
                startPoint = start - stairwell.position, // Local start point relative to stairwell room origin
                endPoint = end - stairwell.position,     // Local end point relative to stairwell room origin
                isExterior = false
                // doorIdsOnWall, windowIdsOnWall, openingIdsOnWall will be null by default, handled by BuildWallSegment
            };

            GameObject wall = BuildWallSegment(tempSegment, stairwell.position, height, thickness, housePlan);
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

    // ProcessWallCutouts method removed
}
