using System.Collections.Generic;
using System.Linq; // Added for Distinct() and OrderBy()
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

                GameObject wallObj = BuildWallSegment(segment, room.position, storyHeight, housePlan.exteriorWallThickness, housePlan); // Added housePlan argument
                if (wallObj != null)
                {
                    wallObj.name = $"Wall_{room.roomId}_{wallIndex}";
                    wallObj.transform.SetParent(root.transform, false);
                    // ProcessWallCutouts(segment, wallObj.name, housePlan); // Removed call
                }
                wallIndex++;
            }
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
    private static GameObject BuildWallSegment(WallSegment segment, Vector3 roomOffset, float storyHeight, float thickness, HousePlanSO housePlan)
    {
        // Corrected: Calculate wallStartWorld, wallEndWorld, wallDirection, and segmentLength first
        Vector3 wallStartWorld = roomOffset + segment.startPoint;
        Vector3 wallEndWorld = roomOffset + segment.endPoint;
        float segmentLength = (wallEndWorld - wallStartWorld).magnitude;

        if (segmentLength <= 0.01f) // Negligible length
        {
            return null;
        }

        // Define directions for rotation (original) and dot products (normalized)
        Vector3 originalDirection = wallEndWorld - wallStartWorld;
        Vector3 wallDirection = originalDirection.normalized; // Moved here, used for dot products

        GameObject wallRoot = new GameObject("WallSegment");
        wallRoot.transform.position = wallStartWorld; // Use new variable
        wallRoot.transform.rotation = Quaternion.FromToRotation(Vector3.right, originalDirection); // Use non-normalized for rotation

        List<float> cutMarkers = new List<float> { 0f, segmentLength }; // Initialize before adding opening markers

        // Gather Openings
        List<DoorSpec> relevantDoors = new List<DoorSpec>();
        List<WindowSpec> relevantWindows = new List<WindowSpec>();
        List<OpeningSpec> relevantOpenings = new List<OpeningSpec>();

        if (housePlan != null) // Ensure housePlan is not null before accessing its lists
        {
            if (segment.doorIdsOnWall != null && housePlan.doors != null)
            {
                foreach (string doorId in segment.doorIdsOnWall)
                {
                    DoorSpec door = housePlan.doors.Find(d => d.doorId == doorId);
                    if (door != null) relevantDoors.Add(door);
                }
            }

            if (segment.windowIdsOnWall != null && housePlan.windows != null)
            {
                foreach (string windowId in segment.windowIdsOnWall)
                {
                    WindowSpec window = housePlan.windows.Find(w => w.windowId == windowId);
                    if (window != null) relevantWindows.Add(window);
                }
            }

            if (segment.openingIdsOnWall != null && housePlan.openings != null)
            {
                foreach (string openingId in segment.openingIdsOnWall)
                {
                    OpeningSpec opening = housePlan.openings.Find(o => o.openingId == openingId);
                    if (opening != null) relevantOpenings.Add(opening);
                }
            }
        }

        // Vector3 wallDirection = (wallEndWorld - wallStartWorld).normalized; // This line is now moved up

        // Add Cut Markers from Openings using corrected logic (wallDirection is already defined)
        foreach (DoorSpec door in relevantDoors)
        {
            Vector3 doorWorldPosition = roomOffset + door.position; // Assuming door.position is relative to roomOffset
            Vector3 vectorFromWallStartToDoor = doorWorldPosition - wallStartWorld;
            float distanceAlongWall = Vector3.Dot(vectorFromWallStartToDoor, wallDirection);
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall, 0f, segmentLength));
            cutMarkers.Add(Mathf.Clamp(distanceAlongWall + door.width, 0f, segmentLength));
        }
        foreach (WindowSpec window in relevantWindows)
        {
            Vector3 windowWorldPosition = roomOffset + window.position; // Assuming window.position is relative to roomOffset
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
                    break; // Found the window for this slice
                }
            }
            if (openingFound) continue; // Move to next slice if window processed

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
                    break; // Found the door for this slice
                }
            }
            if (openingFound) continue; // Move to next slice if door processed

            // Check for other Openings (these are just empty spaces)
            foreach (OpeningSpec opening in relevantOpenings)
            {
                if (sliceMidPoint >= opening.position.x && sliceMidPoint < (opening.position.x + opening.width))
                {
                    openingFound = true; // This part of the wall is an opening, so no mesh needed
                    break; // Found the opening for this slice
                }
            }
            if (openingFound) continue; // Move to next slice if it's a generic opening

            // Solid Wall Slice (if no openings cover this midpoint)
            Mesh solidWallMesh = GenerateWallSliceMesh(sliceLength, storyHeight, thickness, Vector3.zero);
            GameObject solidWallGo = new GameObject("SolidSlice");
            solidWallGo.AddComponent<MeshFilter>().mesh = solidWallMesh;
            solidWallGo.AddComponent<MeshRenderer>(); // TODO: Assign material
            solidWallGo.transform.SetParent(wallRoot.transform, false);
            solidWallGo.transform.localPosition = new Vector3(sliceStart, 0, 0);
        }
        return wallRoot;
    }

    // ProcessWallCutouts method removed
}

