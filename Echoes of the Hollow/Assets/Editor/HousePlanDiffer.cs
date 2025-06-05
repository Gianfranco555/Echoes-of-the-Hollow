using System.Collections.Generic;
using System.Linq;
using UnityEngine; // For Vector3, etc. if used in data structures, though HousePlanSO types should be primary
using UnityEditor; // For AssetDatabase if used for loading
// It's good practice to include HousePlanSO related types if they are directly used or referenced.
// For example, if RoomData, WallSegment etc. from HousePlanSO are used directly in DiffEntry:
// using static HousePlanSO; // Or refer to them with HousePlanSO.RoomData if not using 'static'

// Enum to represent the type of change
public enum ChangeType { Unchanged, Modified, Added, Removed }

// Generic struct to hold information about a single diff entry
public struct DiffEntry<T> {
    public string id; // e.g., roomId, wallId, doorId
    public ChangeType change;
    public T existingData; // Data from the loaded HousePlanSO
    public T capturedData; // Data from the scene capture
    public List<string> differences; // Descriptions of what changed for Modified items

    public DiffEntry(string id, ChangeType change, T existingData, T capturedData, List<string> differences = null)
    {
        this.id = id;
        this.change = change;
        this.existingData = existingData;
        this.capturedData = capturedData;
        this.differences = differences ?? new List<string>();
    }
}

// Class to hold all comparison results
public class DiffResultSet {
    public List<DiffEntry<RoomData>> roomDiffs = new List<DiffEntry<RoomData>>();
    public List<DiffEntry<WallSegment>> wallDiffs = new List<DiffEntry<WallSegment>>();
    public List<DiffEntry<DoorSpec>> doorDiffs = new List<DiffEntry<DoorSpec>>();
    public List<DiffEntry<WindowSpec>> windowDiffs = new List<DiffEntry<WindowSpec>>();
    public List<DiffEntry<OpeningSpec>> openingDiffs = new List<DiffEntry<OpeningSpec>>(); // If openings are also to be diffed

    // Constructor
    public DiffResultSet()
    {
        roomDiffs = new List<DiffEntry<RoomData>>();
        wallDiffs = new List<DiffEntry<WallSegment>>();
        doorDiffs = new List<DiffEntry<DoorSpec>>();
        windowDiffs = new List<DiffEntry<WindowSpec>>();
        openingDiffs = new List<DiffEntry<OpeningSpec>>();
    }
}

// The main static class for differing house plans
public static class HousePlanDiffer
{
    private const float CMP_EPSILON = 0.001f; // Tolerance for float comparisons

    public static HousePlanSO LoadTargetHousePlan(string assetPath = "Assets/BlueprintData/NewHousePlan.asset")
    {
        // Add Debug.Assert here
        Debug.Assert(!string.IsNullOrEmpty(assetPath), "HousePlanDiffer: Asset path is null or empty. Cannot load HousePlanSO.");
        if (string.IsNullOrEmpty(assetPath)) // Keep the original check for robustness, though assert should catch it in editor
        {
            // Optionally, change LogError to LogWarning if assert is primary error mechanism in editor
            Debug.LogError("HousePlanDiffer: Asset path is null or empty. Cannot load HousePlanSO.");
            return null;
        }

        HousePlanSO loadedPlan = AssetDatabase.LoadAssetAtPath<HousePlanSO>(assetPath);

        if (loadedPlan == null)
        {
            Debug.LogError($"HousePlanDiffer: Failed to load HousePlanSO from path: {assetPath}. Make sure the asset exists and the path is correct.");
        }
        // Optionally, log success:
        // else
        // {
        //     Debug.Log($"HousePlanDiffer: Successfully loaded HousePlanSO '{loadedPlan.name}' from path: {assetPath}");
        // }
        return loadedPlan;
    }

    // Add this new helper method
    private static List<KeyValuePair<string, WallSegment>> GetAllWallSegmentsFromRooms(List<RoomData> rooms)
    {
        List<KeyValuePair<string, WallSegment>> allWallsWithIds = new List<KeyValuePair<string, WallSegment>>();
        if (rooms != null)
        {
            foreach (var room in rooms)
            {
                if (string.IsNullOrEmpty(room.roomId))
                {
                    // This case should ideally be handled by the calling capture logic to ensure rooms always have IDs.
                    Debug.LogWarning($"HousePlanDiffer: Captured room '{room.roomLabel}' has a null or empty roomId. Walls from this room will use 'CAPTURED_UNKNOWN_ROOM' for ID generation.");
                }
                if (room.walls != null)
                {
                    for (int i = 0; i < room.walls.Count; i++)
                    {
                        string roomIdPart = string.IsNullOrEmpty(room.roomId) ? "CAPTURED_UNKNOWN_ROOM" : room.roomId;
                        string wallId = $"{roomIdPart}_Wall{i}";
                        allWallsWithIds.Add(new KeyValuePair<string, WallSegment>(wallId, room.walls[i]));
                    }
                }
            }
        }
        return allWallsWithIds;
    }

    public static DiffResultSet ComparePlanToScene(
        HousePlanSO existingPlan,
        List<RoomData> capturedRooms,
        List<DoorSpec> capturedDoors,
        List<WindowSpec> capturedWindows,
        List<OpeningSpec> capturedOpenings,
        List<WallSegment> sceneWalls // New parameter
        )
    {
        if (existingPlan == null)
        {
            Debug.LogError("HousePlanDiffer: Existing HousePlanSO is null. Cannot perform comparison.");
            return null;
        }
        // Null checks for captured lists
        if (capturedRooms == null) { Debug.LogWarning("HousePlanDiffer: Captured rooms list is null. Room comparison will be limited."); capturedRooms = new List<RoomData>(); }
        if (sceneWalls == null) { Debug.LogWarning("HousePlanDiffer: Captured sceneWalls list is null. Wall comparison will be limited."); sceneWalls = new List<WallSegment>(); } // Added null check for sceneWalls
        if (capturedDoors == null) { Debug.LogWarning("HousePlanDiffer: Captured doors list is null. Door comparison will be limited."); capturedDoors = new List<DoorSpec>(); }
        if (capturedWindows == null) { Debug.LogWarning("HousePlanDiffer: Captured windows list is null. Window comparison will be limited."); capturedWindows = new List<WindowSpec>(); }
        if (capturedOpenings == null) { Debug.LogWarning("HousePlanDiffer: Captured openings list is null. Opening comparison will be limited."); capturedOpenings = new List<OpeningSpec>(); }

        Debug.Log($"HousePlanDiffer: Starting comparison between existing plan '{existingPlan.name}' and captured data.");
        DiffResultSet resultSet = new DiffResultSet();

        // Compare Rooms
        CompareRooms(existingPlan.rooms, capturedRooms, resultSet.roomDiffs);

        // Compare Walls
        List<KeyValuePair<string, WallSegment>> targetWallEntries = GetAllWallSegments(existingPlan);
        // Use GenerateWallEntries with the new sceneWalls parameter
        List<KeyValuePair<string, WallSegment>> capturedWallEntries = GenerateWallEntries(sceneWalls, "CAPTURED_WALL");
        CompareWalls(targetWallEntries, capturedWallEntries, resultSet.wallDiffs);

        // Compare Doors
        CompareDoors(existingPlan.doors, capturedDoors, resultSet.doorDiffs);

        // Compare Windows
        CompareWindows(existingPlan.windows, capturedWindows, resultSet.windowDiffs);

        // Compare Openings
        CompareOpenings(existingPlan.openings, capturedOpenings, resultSet.openingDiffs);

        Debug.Log("HousePlanDiffer: Comparison finished.");
        return resultSet;
    }

    private static List<KeyValuePair<string, WallSegment>> GetAllWallSegments(HousePlanSO plan)
    {
        List<KeyValuePair<string, WallSegment>> allWallsWithIds = new List<KeyValuePair<string, WallSegment>>();
        if (plan != null && plan.rooms != null)
        {
            foreach (var room in plan.rooms)
            {
                if (string.IsNullOrEmpty(room.roomId))
                {
                    Debug.LogWarning($"HousePlanDiffer: Room '{room.roomLabel}' has a null or empty roomId. Walls from this room will use 'UNKNOWN_ROOM' for ID generation.");
                }
                if (room.walls != null)
                {
                    for (int i = 0; i < room.walls.Count; i++)
                    {
                        string roomIdPart = string.IsNullOrEmpty(room.roomId) ? "UNKNOWN_ROOM" : room.roomId;
                        string wallId = $"{roomIdPart}_Wall{i}";
                        allWallsWithIds.Add(new KeyValuePair<string, WallSegment>(wallId, room.walls[i]));
                    }
                }
            }
        }
        return allWallsWithIds;
    }

    private static void CompareRooms(List<RoomData> targetRooms, List<RoomData> capturedRooms, List<DiffEntry<RoomData>> diffList)
    {
        Debug.Log($"HousePlanDiffer: Comparing rooms. Target count: {targetRooms?.Count ?? 0}, Captured count: {capturedRooms?.Count ?? 0}");
        diffList.Clear();

        Dictionary<string, RoomData> capturedRoomsDict = new Dictionary<string, RoomData>();
        if (capturedRooms != null)
        {
            foreach (var room in capturedRooms)
            {
                if (!string.IsNullOrEmpty(room.roomId))
                {
                    capturedRoomsDict[room.roomId] = room;
                }
                else
                {
                    // Handle rooms without IDs - potentially treat as new if no matching unnamed room exists in target.
                    // For now, we'll log a warning and they might be missed in comparison if target also has unnamed rooms.
                    Debug.LogWarning("Captured room found with no ID. This room might not be compared correctly.");
                }
            }
        }

        // Check for modified and removed rooms
        if (targetRooms != null)
        {
            foreach (var targetRoom in targetRooms)
            {
                if (string.IsNullOrEmpty(targetRoom.roomId))
                {
                    Debug.LogWarning($"Target room found with no ID: '{targetRoom.roomLabel}'. This room might not be compared correctly.");
                    // Potentially treat as a special case or require all target rooms to have IDs.
                    continue;
                }

                if (capturedRoomsDict.TryGetValue(targetRoom.roomId, out RoomData capturedRoom))
                {
                    List<string> differences = new List<string>();
                    if (targetRoom.roomLabel != capturedRoom.roomLabel)
                        differences.Add($"Label changed from '{targetRoom.roomLabel}' to '{capturedRoom.roomLabel}'.");
                    if (Vector2.Distance(targetRoom.dimensions, capturedRoom.dimensions) > CMP_EPSILON)
                        differences.Add($"Dimensions changed from {targetRoom.dimensions.ToString("F3")} to {capturedRoom.dimensions.ToString("F3")}.");
                    if (Vector3.Distance(targetRoom.position, capturedRoom.position) > CMP_EPSILON)
                        differences.Add($"Position changed from {targetRoom.position.ToString("F3")} to {capturedRoom.position.ToString("F3")}.");
                    if (targetRoom.notes != capturedRoom.notes)
                         differences.Add($"Notes changed from '{targetRoom.notes}' to '{capturedRoom.notes}'.");
                    if (!AreEquivalentStringLists(targetRoom.connectedRoomIds, capturedRoom.connectedRoomIds))
                        differences.Add($"Connected room IDs changed from [{string.Join(", ", targetRoom.connectedRoomIds ?? new List<string>())}] to [{string.Join(", ", capturedRoom.connectedRoomIds ?? new List<string>())}].");
                    if ((targetRoom.walls?.Count ?? 0) != (capturedRoom.walls?.Count ?? 0)) // Simple wall count check
                        differences.Add($"Wall count changed from {targetRoom.walls?.Count ?? 0} to {capturedRoom.walls?.Count ?? 0}.");
                    // Deeper wall segment comparison is handled by CompareWalls if walls are globally compared.
                    // If walls are only relevant *within* a room and not globally, more logic would be needed here.

                    if (differences.Count > 0)
                    {
                        diffList.Add(new DiffEntry<RoomData>(targetRoom.roomId, ChangeType.Modified, targetRoom, capturedRoom, differences));
                    }
                    else
                    {
                        diffList.Add(new DiffEntry<RoomData>(targetRoom.roomId, ChangeType.Unchanged, targetRoom, capturedRoom));
                    }
                    capturedRoomsDict.Remove(targetRoom.roomId); // Remove from dict as it's been processed
                }
                else
                {
                    diffList.Add(new DiffEntry<RoomData>(targetRoom.roomId, ChangeType.Removed, targetRoom, default(RoomData)));
                }
            }
        }

        // Any rooms left in capturedRoomsDict are new (added)
        foreach (var capturedRoomEntry in capturedRoomsDict)
        {
            diffList.Add(new DiffEntry<RoomData>(capturedRoomEntry.Key, ChangeType.Added, default(RoomData), capturedRoomEntry.Value));
        }
        Debug.Log($"HousePlanDiffer: Room comparison finished. Diffs found: {diffList.Count}");
    }

    // Helper method to compare two lists of strings for equivalency (ignoring order for simplicity here)
    private static bool AreEquivalentStringLists(List<string> list1, List<string> list2)
    {
        var s1 = list1 ?? new List<string>();
        var s2 = list2 ?? new List<string>();
        if (s1.Count != s2.Count) return false;
        var set1 = new HashSet<string>(s1);
        foreach (var item in s2)
        {
            if (!set1.Contains(item)) return false;
        }
        return true;
    }

    private static List<KeyValuePair<string, WallSegment>> GenerateWallEntries(List<WallSegment> walls, string idPrefix)
    {
        List<KeyValuePair<string, WallSegment>> wallEntries = new List<KeyValuePair<string, WallSegment>>();
        if (walls != null)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                // The ID here is for diffing purposes.
                // If WallSegment has its own ID field populated from capture (e.g., from GameObject name),
                // that should ideally be used. For now, generating a temporary one.
                string wallId = $"{idPrefix}_{i}";
                wallEntries.Add(new KeyValuePair<string, WallSegment>(wallId, walls[i]));
            }
        }
        return wallEntries;
    }

// New CompareWalls structure:
private static void CompareWalls(
    List<KeyValuePair<string, WallSegment>> targetWallEntries,
    List<KeyValuePair<string, WallSegment>> capturedWallEntries,
    List<DiffEntry<WallSegment>> diffList)
{
    Debug.Log($"HousePlanDiffer: Comparing walls. Target count: {targetWallEntries?.Count ?? 0}, Captured count: {capturedWallEntries?.Count ?? 0}");
    diffList.Clear();

    Dictionary<string, WallSegment> capturedWallsDict = capturedWallEntries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    List<KeyValuePair<string, WallSegment>> unmatchedTargetWalls = new List<KeyValuePair<string, WallSegment>>();

    // 1. Match by ID
    foreach (var targetEntry in targetWallEntries)
    {
        if (capturedWallsDict.TryGetValue(targetEntry.Key, out WallSegment capturedWall))
        {
            List<string> differences = new List<string>();
            CompareWallProperties(targetEntry.Value, capturedWall, differences); // Helper for property comparison

            if (differences.Count > 0)
            {
                diffList.Add(new DiffEntry<WallSegment>(targetEntry.Key, ChangeType.Modified, targetEntry.Value, capturedWall, differences));
            }
            else
            {
                diffList.Add(new DiffEntry<WallSegment>(targetEntry.Key, ChangeType.Unchanged, targetEntry.Value, capturedWall));
            }
            capturedWallsDict.Remove(targetEntry.Key); // Matched
        }
        else
        {
            unmatchedTargetWalls.Add(targetEntry); // No ID match
        }
    }

    // 2. Tolerant Spatial Matching for Unmatched Walls
    List<KeyValuePair<string, WallSegment>> remainingCapturedWallEntries = capturedWallsDict.Select(kvp => new KeyValuePair<string, WallSegment>(kvp.Key, kvp.Value)).ToList();
    List<KeyValuePair<string, WallSegment>> spatiallyMatchedTargetWalls = new List<KeyValuePair<string, WallSegment>>();
    List<KeyValuePair<string, WallSegment>> spatiallyMatchedCapturedWalls = new List<KeyValuePair<string, WallSegment>>();

    const float SPATIAL_MATCH_TOLERANCE = 0.1f; // 10cm tolerance for start/end points to be considered a match

    foreach (var targetEntry in unmatchedTargetWalls)
    {
        KeyValuePair<string, WallSegment> bestMatchCapturedEntry = default;
        float minDistance = float.MaxValue;
        bool foundSpatialMatch = false;

        // Need to iterate using index to allow safe removal or use a temporary list for matches
        for (int i = 0; i < remainingCapturedWallEntries.Count; i++)
        {
            var capturedEntry = remainingCapturedWallEntries[i];
            // Skip if this captured wall has already been spatially matched to another target wall
            if (spatiallyMatchedCapturedWalls.Any(kvp => kvp.Key == capturedEntry.Key)) continue;

            float distStart = Vector3.Distance(targetEntry.Value.startPoint, capturedEntry.Value.startPoint);
            float distEnd = Vector3.Distance(targetEntry.Value.endPoint, capturedEntry.Value.endPoint);

            if (distStart < SPATIAL_MATCH_TOLERANCE && distEnd < SPATIAL_MATCH_TOLERANCE)
            {
                if ((distStart + distEnd) < minDistance)
                {
                    minDistance = distStart + distEnd;
                    bestMatchCapturedEntry = capturedEntry;
                    foundSpatialMatch = true;
                }
            }
        }

        if (foundSpatialMatch)
        {
            List<string> differences = new List<string>();
            differences.Add($"Spatially matched. Target ID '{targetEntry.Key}' matched to captured wall originally identified as '{bestMatchCapturedEntry.Key}'.");
            CompareWallProperties(targetEntry.Value, bestMatchCapturedEntry.Value, differences);

            diffList.Add(new DiffEntry<WallSegment>(targetEntry.Key, ChangeType.Modified, targetEntry.Value, bestMatchCapturedEntry.Value, differences));

            spatiallyMatchedTargetWalls.Add(targetEntry);
            spatiallyMatchedCapturedWalls.Add(bestMatchCapturedEntry);
        }
    }

    // Filter out the spatially matched target walls from the unmatchedTargetWalls list
    unmatchedTargetWalls = unmatchedTargetWalls.Where(t => !spatiallyMatchedTargetWalls.Any(smt => smt.Key == t.Key)).ToList();

    // Filter out the spatially matched captured walls from the remaining captured entries before declaring them as "Added"
    var finalCapturedWallsDict = new Dictionary<string, WallSegment>();
    foreach(var entry in remainingCapturedWallEntries)
    {
        if (!spatiallyMatchedCapturedWalls.Any(smc => smc.Key == entry.Key))
        {
            finalCapturedWallsDict[entry.Key] = entry.Value;
        }
    }

    // 3. Process remaining as Added or Removed
    foreach (var targetEntry in unmatchedTargetWalls) // These are targets that had no ID match and no spatial match
    {
        diffList.Add(new DiffEntry<WallSegment>(targetEntry.Key, ChangeType.Removed, targetEntry.Value, default(WallSegment)));
    }

    foreach (var capturedEntryKvp in finalCapturedWallsDict) // These are captured walls that had no ID match and were not spatially matched
    {
        diffList.Add(new DiffEntry<WallSegment>(capturedEntryKvp.Key, ChangeType.Added, default(WallSegment), capturedEntryKvp.Value));
    }

    Debug.Log($"HousePlanDiffer: Wall comparison finished. Diffs found: {diffList.Count}");
}

// Helper method for comparing properties of two WallSegments
private static void CompareWallProperties(WallSegment targetWall, WallSegment capturedWall, List<string> differences)
{
    if (!Mathf.Approximately(targetWall.thickness, capturedWall.thickness))
        differences.Add($"Thickness changed from {targetWall.thickness.ToString("F3")} to {capturedWall.thickness.ToString("F3")}.");
    if (targetWall.isExterior != capturedWall.isExterior)
        differences.Add($"IsExterior changed from {targetWall.isExterior} to {capturedWall.isExterior}.");
    if (targetWall.side != capturedWall.side)
        differences.Add($"Side changed from {targetWall.side} to {capturedWall.side}.");

    if (!AreEquivalentStringLists(targetWall.doorIdsOnWall, capturedWall.doorIdsOnWall))
        differences.Add($"Door IDs on wall changed. Existing: [{string.Join(", ", targetWall.doorIdsOnWall ?? new List<string>())}], Captured: [{string.Join(", ", capturedWall.doorIdsOnWall ?? new List<string>())}]");
    if (!AreEquivalentStringLists(targetWall.windowIdsOnWall, capturedWall.windowIdsOnWall))
        differences.Add($"Window IDs on wall changed. Existing: [{string.Join(", ", targetWall.windowIdsOnWall ?? new List<string>())}], Captured: [{string.Join(", ", capturedWall.windowIdsOnWall ?? new List<string>())}]");
    if (!AreEquivalentStringLists(targetWall.openingIdsOnWall, capturedWall.openingIdsOnWall))
        differences.Add($"Opening IDs on wall changed. Existing: [{string.Join(", ", targetWall.openingIdsOnWall ?? new List<string>())}], Captured: [{string.Join(", ", capturedWall.openingIdsOnWall ?? new List<string>())}]");

    // This check is important for spatially matched walls to show how close their points are.
    // For ID-matched walls, it would only trigger if points changed but ID (Room_Index) somehow remained same (unlikely).
    if (Vector3.Distance(targetWall.startPoint, capturedWall.startPoint) > CMP_EPSILON || Vector3.Distance(targetWall.endPoint, capturedWall.endPoint) > CMP_EPSILON)
    {
        differences.Add($"Points differ (even if spatially matched within tolerance). Start: {targetWall.startPoint.ToString("F3")}->{capturedWall.startPoint.ToString("F3")}, End: {targetWall.endPoint.ToString("F3")}->{capturedWall.endPoint.ToString("F3")}");
    }
}
    // The RoundVector3 method is no longer needed by CompareWalls with the new ID strategy.
    // It can be removed if not used elsewhere in this class.
    // private static Vector3 RoundVector3(Vector3 vector, int decimalPlaces)
    // {
    //     float multiplier = Mathf.Pow(10, decimalPlaces);
    //     return new Vector3(
    //         Mathf.Round(vector.x * multiplier) / multiplier,
    //         Mathf.Round(vector.y * multiplier) / multiplier,
    //         Mathf.Round(vector.z * multiplier) / multiplier
    //     );
    // }

    private static void CompareDoors(List<DoorSpec> targetDoors, List<DoorSpec> capturedDoors, List<DiffEntry<DoorSpec>> diffList)
    {
        Debug.Log($"HousePlanDiffer: Comparing doors. Target count: {targetDoors?.Count ?? 0}, Captured count: {capturedDoors?.Count ?? 0}");
        diffList.Clear();

        Dictionary<string, DoorSpec> capturedDoorsDict = new Dictionary<string, DoorSpec>();
        if (capturedDoors != null)
        {
            foreach (var door in capturedDoors)
            {
                if (!string.IsNullOrEmpty(door.doorId))
                {
                    capturedDoorsDict[door.doorId] = door;
                }
                else
                {
                    Debug.LogWarning("Captured door found with no ID. This door might not be compared correctly.");
                }
            }
        }

        if (targetDoors != null)
        {
            foreach (var targetDoor in targetDoors)
            {
                if (string.IsNullOrEmpty(targetDoor.doorId))
                {
                    Debug.LogWarning($"Target door found with no ID. This door might not be compared correctly.");
                    continue;
                }

                if (capturedDoorsDict.TryGetValue(targetDoor.doorId, out DoorSpec capturedDoor))
                {
                    List<string> differences = new List<string>();

                    if (targetDoor.type != capturedDoor.type)
                        differences.Add($"Type changed from '{targetDoor.type}' to '{capturedDoor.type}'.");
                    if (!Mathf.Approximately(targetDoor.width, capturedDoor.width))
                        differences.Add($"Width changed from {targetDoor.width.ToString("F3")} to {capturedDoor.width.ToString("F3")}.");
                    if (!Mathf.Approximately(targetDoor.height, capturedDoor.height))
                        differences.Add($"Height changed from {targetDoor.height.ToString("F3")} to {capturedDoor.height.ToString("F3")}.");
                    if (Vector3.Distance(targetDoor.position, capturedDoor.position) > CMP_EPSILON)
                        differences.Add($"Position changed from {targetDoor.position.ToString("F3")} to {capturedDoor.position.ToString("F3")}.");
                    if (targetDoor.wallId != capturedDoor.wallId)
                        differences.Add($"WallId changed from '{targetDoor.wallId}' to '{capturedDoor.wallId}'.");
                    if (targetDoor.swingDirection != capturedDoor.swingDirection)
                        differences.Add($"SwingDirection changed from '{targetDoor.swingDirection}' to '{capturedDoor.swingDirection}'.");
                    if (targetDoor.slideDirection != capturedDoor.slideDirection)
                        differences.Add($"SlideDirection changed from '{targetDoor.slideDirection}' to '{capturedDoor.slideDirection}'.");
                    if (targetDoor.isExterior != capturedDoor.isExterior)
                        differences.Add($"IsExterior changed from {targetDoor.isExterior} to {capturedDoor.isExterior}.");
                    if (targetDoor.connectsRoomA_Id != capturedDoor.connectsRoomA_Id)
                        differences.Add($"ConnectsRoomA_Id changed from '{targetDoor.connectsRoomA_Id}' to '{capturedDoor.connectsRoomA_Id}'.");
                    if (targetDoor.connectsRoomB_Id != capturedDoor.connectsRoomB_Id)
                        differences.Add($"ConnectsRoomB_Id changed from '{targetDoor.connectsRoomB_Id}' to '{capturedDoor.connectsRoomB_Id}'.");

                    if (differences.Count > 0)
                    {
                        diffList.Add(new DiffEntry<DoorSpec>(targetDoor.doorId, ChangeType.Modified, targetDoor, capturedDoor, differences));
                    }
                    else
                    {
                        diffList.Add(new DiffEntry<DoorSpec>(targetDoor.doorId, ChangeType.Unchanged, targetDoor, capturedDoor));
                    }
                    capturedDoorsDict.Remove(targetDoor.doorId); // Processed
                }
                else
                {
                    diffList.Add(new DiffEntry<DoorSpec>(targetDoor.doorId, ChangeType.Removed, targetDoor, default(DoorSpec)));
                }
            }
        }

        // Any doors left in capturedDoorsDict are new (added)
        foreach (var capturedDoorEntry in capturedDoorsDict)
        {
            diffList.Add(new DiffEntry<DoorSpec>(capturedDoorEntry.Key, ChangeType.Added, default(DoorSpec), capturedDoorEntry.Value));
        }
        Debug.Log($"HousePlanDiffer: Door comparison finished. Diffs found: {diffList.Count}");
    }

    private static void CompareWindows(List<WindowSpec> targetWindows, List<WindowSpec> capturedWindows, List<DiffEntry<WindowSpec>> diffList)
    {
        Debug.Log($"HousePlanDiffer: Comparing windows. Target count: {targetWindows?.Count ?? 0}, Captured count: {capturedWindows?.Count ?? 0}");
        diffList.Clear();

        Dictionary<string, WindowSpec> capturedWindowsDict = new Dictionary<string, WindowSpec>();
        if (capturedWindows != null)
        {
            foreach (var window in capturedWindows)
            {
                if (!string.IsNullOrEmpty(window.windowId))
                {
                    capturedWindowsDict[window.windowId] = window;
                }
                else
                {
                    Debug.LogWarning("Captured window found with no ID. This window might not be compared correctly.");
                }
            }
        }

        if (targetWindows != null)
        {
            foreach (var targetWindow in targetWindows)
            {
                if (string.IsNullOrEmpty(targetWindow.windowId))
                {
                    Debug.LogWarning($"Target window found with no ID. This window might not be compared correctly.");
                    continue;
                }

                if (capturedWindowsDict.TryGetValue(targetWindow.windowId, out WindowSpec capturedWindow))
                {
                    List<string> differences = new List<string>();

                    if (targetWindow.type != capturedWindow.type)
                        differences.Add($"Type changed from '{targetWindow.type}' to '{capturedWindow.type}'.");
                    if (!Mathf.Approximately(targetWindow.width, capturedWindow.width))
                        differences.Add($"Width changed from {targetWindow.width.ToString("F3")} to {capturedWindow.width.ToString("F3")}.");
                    if (!Mathf.Approximately(targetWindow.height, capturedWindow.height))
                        differences.Add($"Height changed from {targetWindow.height.ToString("F3")} to {capturedWindow.height.ToString("F3")}.");
                    if (Vector3.Distance(targetWindow.position, capturedWindow.position) > CMP_EPSILON)
                        differences.Add($"Position changed from {targetWindow.position.ToString("F3")} to {capturedWindow.position.ToString("F3")}.");
                    if (!Mathf.Approximately(targetWindow.sillHeight, capturedWindow.sillHeight))
                        differences.Add($"SillHeight changed from {targetWindow.sillHeight.ToString("F3")} to {capturedWindow.sillHeight.ToString("F3")}.");
                    if (targetWindow.wallId != capturedWindow.wallId)
                        differences.Add($"WallId changed from '{targetWindow.wallId}' to '{capturedWindow.wallId}'.");
                    if (targetWindow.isOperable != capturedWindow.isOperable)
                        differences.Add($"IsOperable changed from {targetWindow.isOperable} to {capturedWindow.isOperable}.");

                    if (targetWindow.type == WindowType.Bay || capturedWindow.type == WindowType.Bay)
                    {
                        if (targetWindow.bayPanes != capturedWindow.bayPanes) // int comparison
                            differences.Add($"BayPanes changed from {targetWindow.bayPanes} to {capturedWindow.bayPanes}.");
                        if (!Mathf.Approximately(targetWindow.bayProjectionDepth, capturedWindow.bayProjectionDepth))
                            differences.Add($"BayProjectionDepth changed from {targetWindow.bayProjectionDepth.ToString("F3")} to {capturedWindow.bayProjectionDepth.ToString("F3")}.");
                    }

                    if (differences.Count > 0)
                    {
                        diffList.Add(new DiffEntry<WindowSpec>(targetWindow.windowId, ChangeType.Modified, targetWindow, capturedWindow, differences));
                    }
                    else
                    {
                        diffList.Add(new DiffEntry<WindowSpec>(targetWindow.windowId, ChangeType.Unchanged, targetWindow, capturedWindow));
                    }
                    capturedWindowsDict.Remove(targetWindow.windowId); // Processed
                }
                else
                {
                    diffList.Add(new DiffEntry<WindowSpec>(targetWindow.windowId, ChangeType.Removed, targetWindow, default(WindowSpec)));
                }
            }
        }

        // Any windows left in capturedWindowsDict are new (added)
        foreach (var capturedWindowEntry in capturedWindowsDict)
        {
            diffList.Add(new DiffEntry<WindowSpec>(capturedWindowEntry.Key, ChangeType.Added, default(WindowSpec), capturedWindowEntry.Value));
        }
        Debug.Log($"HousePlanDiffer: Window comparison finished. Diffs found: {diffList.Count}");
    }

    private static void CompareOpenings(List<OpeningSpec> targetOpenings, List<OpeningSpec> capturedOpenings, List<DiffEntry<OpeningSpec>> diffList)
    {
        Debug.Log($"HousePlanDiffer: Comparing openings. Target count: {targetOpenings?.Count ?? 0}, Captured count: {capturedOpenings?.Count ?? 0}");
        diffList.Clear();

        Dictionary<string, OpeningSpec> capturedOpeningsDict = new Dictionary<string, OpeningSpec>();
        if (capturedOpenings != null)
        {
            foreach (var opening in capturedOpenings)
            {
                if (!string.IsNullOrEmpty(opening.openingId))
                {
                    capturedOpeningsDict[opening.openingId] = opening;
                }
                else
                {
                    Debug.LogWarning("Captured opening found with no ID. This opening might not be compared correctly.");
                }
            }
        }

        if (targetOpenings != null)
        {
            foreach (var targetOpening in targetOpenings)
            {
                if (string.IsNullOrEmpty(targetOpening.openingId))
                {
                    Debug.LogWarning($"Target opening found with no ID. This opening might not be compared correctly.");
                    continue;
                }

                if (capturedOpeningsDict.TryGetValue(targetOpening.openingId, out OpeningSpec capturedOpening))
                {
                    List<string> differences = new List<string>();

                    if (targetOpening.type != capturedOpening.type)
                        differences.Add($"Type changed from '{targetOpening.type}' to '{capturedOpening.type}'.");
                    if (!Mathf.Approximately(targetOpening.width, capturedOpening.width))
                        differences.Add($"Width changed from {targetOpening.width.ToString("F3")} to {capturedOpening.width.ToString("F3")}.");
                    if (!Mathf.Approximately(targetOpening.height, capturedOpening.height))
                        differences.Add($"Height changed from {targetOpening.height.ToString("F3")} to {capturedOpening.height.ToString("F3")}.");
                    if (Vector3.Distance(targetOpening.position, capturedOpening.position) > CMP_EPSILON)
                        differences.Add($"Position changed from {targetOpening.position.ToString("F3")} to {capturedOpening.position.ToString("F3")}.");
                    if (targetOpening.wallId != capturedOpening.wallId)
                        differences.Add($"WallId changed from '{targetOpening.wallId}' to '{capturedOpening.wallId}'.");
                    if (!Mathf.Approximately(targetOpening.passthroughLedgeDepth, capturedOpening.passthroughLedgeDepth))
                        differences.Add($"PassthroughLedgeDepth changed from {targetOpening.passthroughLedgeDepth.ToString("F3")} to {capturedOpening.passthroughLedgeDepth.ToString("F3")}.");
                    if (targetOpening.connectsRoomA_Id != capturedOpening.connectsRoomA_Id)
                        differences.Add($"ConnectsRoomA_Id changed from '{targetOpening.connectsRoomA_Id}' to '{capturedOpening.connectsRoomA_Id}'.");
                    if (targetOpening.connectsRoomB_Id != capturedOpening.connectsRoomB_Id)
                        differences.Add($"ConnectsRoomB_Id changed from '{targetOpening.connectsRoomB_Id}' to '{capturedOpening.connectsRoomB_Id}'.");

                    if (differences.Count > 0)
                    {
                        diffList.Add(new DiffEntry<OpeningSpec>(targetOpening.openingId, ChangeType.Modified, targetOpening, capturedOpening, differences));
                    }
                    else
                    {
                        diffList.Add(new DiffEntry<OpeningSpec>(targetOpening.openingId, ChangeType.Unchanged, targetOpening, capturedOpening));
                    }
                    capturedOpeningsDict.Remove(targetOpening.openingId); // Processed
                }
                else
                {
                    diffList.Add(new DiffEntry<OpeningSpec>(targetOpening.openingId, ChangeType.Removed, targetOpening, default(OpeningSpec)));
                }
            }
        }

        // Any openings left in capturedOpeningsDict are new (added)
        foreach (var capturedOpeningEntry in capturedOpeningsDict)
        {
            diffList.Add(new DiffEntry<OpeningSpec>(capturedOpeningEntry.Key, ChangeType.Added, default(OpeningSpec), capturedOpeningEntry.Value));
        }
        Debug.Log($"HousePlanDiffer: Opening comparison finished. Diffs found: {diffList.Count}");
    }

    // Methods will be added in subsequent steps based on the plan
    // e.g., LoadTargetHousePlan, ComparePlanToScene
}
