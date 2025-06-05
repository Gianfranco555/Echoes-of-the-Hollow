// Placeholder Enums (to be defined properly if they exist elsewhere or requirements are clarified)
public enum DoorType { Hinged, Sliding, Pocket, Other }
public enum SwingDirection { InwardNorth, InwardSouth, InwardEast, InwardWest, OutwardNorth, OutwardSouth, OutwardEast, OutwardWest, None }
public enum SlideDirection { SlidesLeft, SlidesRight, SlidesUp, SlidesDown, None }
public enum WindowType { SingleHung, DoubleHung, Casement, Sliding, Picture, Bay, Bow, Other }

using UnityEditor;
using UnityEngine;
using System.Text;
using System.Globalization; // Add this line
using System.Collections.Generic; // For List<T>
using System.Linq; // For Linq operations

public class TransformCaptureWindow : EditorWindow
{
    public enum HouseComponentType { Unknown, Room, Wall, Door, Window, Foundation, Roof, ProceduralHouseRoot }
    public enum CoordinateSpaceSetting { World, RoomRelative, WallRelative }
    public enum CaptureMode
    {
        SelectedObjects,
        ActiveScene_AllHouseComponents,
        ActiveScene_RoomsOnly,
        ActiveScene_WallsOnly,
        ActiveScene_DoorsAndWindowsOnly
    }
    private CaptureMode captureMode = CaptureMode.SelectedObjects;

    private string generatedCode = "";
    private Vector2 scrollPosition;
    private CoordinateSpaceSetting selectedCoordinateSpace = CoordinateSpaceSetting.World;
    private bool useContextualFormatting = false; // Added field
    private bool groupByRoom = false;

    [MenuItem("House Tools/Transform Data Capturer")]
    public static void ShowWindow()
    {
        GetWindow<TransformCaptureWindow>("Transform Capturer");
    }

    void OnGUI()
    {
        selectedCoordinateSpace = (CoordinateSpaceSetting)EditorGUILayout.EnumPopup("Coordinate Space:", selectedCoordinateSpace);
        // Add this before the capture button
        useContextualFormatting = EditorGUILayout.Toggle("Use Contextual House Formatting", useContextualFormatting);
        captureMode = (CaptureMode)EditorGUILayout.EnumPopup("Capture Mode:", captureMode);
        EditorGUI.BeginDisabledGroup(!useContextualFormatting);
        groupByRoom = EditorGUILayout.Toggle("Group by Room", groupByRoom);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Capture Selected Transforms"))
        {
            CaptureTransforms();
        }

        EditorGUILayout.LabelField("Generated C# Code:", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        // Make TextArea read-only by using a style with normal.textColor set to a non-editable look,
        // or by simply not providing a way to change `generatedCode` other than CaptureTransforms.
        // For actual read-only behavior, one might use EditorGUI.SelectableLabel.
        // However, TextArea is fine for typical editor script usage if modification isn't intended.
        EditorGUILayout.TextArea(generatedCode, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndScrollView();
    }

    private void CaptureTransforms()
    {
        StringBuilder sb = new StringBuilder();
        List<GameObject> objectsToProcess = new List<GameObject>();

        // 1. Get GameObjects based on captureMode
        switch (captureMode)
        {
            case CaptureMode.SelectedObjects:
                objectsToProcess.AddRange(Selection.gameObjects);
                break;
            case CaptureMode.ActiveScene_AllHouseComponents:
                objectsToProcess.AddRange(FindAllHouseComponents());
                break;
            case CaptureMode.ActiveScene_RoomsOnly:
                objectsToProcess.AddRange(FindAllHouseComponents(HouseComponentType.Room));
                break;
            case CaptureMode.ActiveScene_WallsOnly:
                objectsToProcess.AddRange(FindAllHouseComponents(HouseComponentType.Wall));
                break;
            case CaptureMode.ActiveScene_DoorsAndWindowsOnly:
                objectsToProcess.AddRange(FindAllHouseComponents(HouseComponentType.Door));
                objectsToProcess.AddRange(FindAllHouseComponents(HouseComponentType.Window));
                break;
        }

        if (objectsToProcess.Count == 0)
        {
            sb.AppendLine("// No objects found or selected for capture.");
            generatedCode = sb.ToString();
            Repaint(); // Repaint to show update
            return;
        }

        // 2. Output Logic
        if (useContextualFormatting && groupByRoom)
        {
            Dictionary<GameObject, List<GameObject>> roomMap = new Dictionary<GameObject, List<GameObject>>();
            List<GameObject> unassignedComponents = new List<GameObject>();
            List<GameObject> allRooms = new List<GameObject>(); // To maintain order and process rooms first

            List<GameObject> roomsToConsiderForMap = new List<GameObject>();
            if (captureMode == CaptureMode.ActiveScene_AllHouseComponents || captureMode == CaptureMode.ActiveScene_RoomsOnly)
            {
                roomsToConsiderForMap.AddRange(objectsToProcess.Where(obj => DetectComponentType(obj) == HouseComponentType.Room));
            }
            else
            {
                roomsToConsiderForMap.AddRange(FindAllHouseComponents(HouseComponentType.Room));
            }

            foreach(GameObject roomObj in roomsToConsiderForMap.Distinct())
            {
                if (!roomMap.ContainsKey(roomObj))
                {
                    allRooms.Add(roomObj);
                    roomMap[roomObj] = new List<GameObject>();
                }
            }
            allRooms = allRooms.OrderBy(r => r.name).ToList();

            foreach (GameObject obj in objectsToProcess.Distinct())
            {
                HouseComponentType currentObjType = DetectComponentType(obj);
                if (currentObjType == HouseComponentType.Room)
                {
                    if (!roomMap.ContainsKey(obj)) {
                        allRooms.Add(obj);
                        allRooms = allRooms.OrderBy(r => r.name).ToList();
                        roomMap[obj] = new List<GameObject>();
                    }
                    // Room data itself is formatted when iterating through allRooms.
                    continue;
                }

                GameObject roomContext = GetRoomContext(obj);
                if (roomContext != null && roomMap.ContainsKey(roomContext))
                {
                    roomMap[roomContext].Add(obj);
                }
                else
                {
                    unassignedComponents.Add(obj);
                }
            }

            bool shouldCaptureRoomDataItself = captureMode == CaptureMode.ActiveScene_AllHouseComponents ||
                                               captureMode == CaptureMode.ActiveScene_RoomsOnly ||
                                               (captureMode == CaptureMode.SelectedObjects && objectsToProcess.Any(o => DetectComponentType(o) == HouseComponentType.Room));

            foreach (GameObject roomObj in allRooms)
            {
                bool roomHasRelevantChildren = roomMap.ContainsKey(roomObj) && roomMap[roomObj].Count > 0;
                bool roomIsTargetOfCapture = objectsToProcess.Contains(roomObj);

                if (!roomIsTargetOfCapture && !roomHasRelevantChildren) continue;

                sb.AppendLine($"// --- Room: {roomObj.name} (World Position: {roomObj.transform.position.ToString("F2", CultureInfo.InvariantCulture)}) ---");

                if (shouldCaptureRoomDataItself && roomIsTargetOfCapture)
                {
                     sb.AppendLine(FormatAsRoomData(roomObj));
                }

                if (roomMap.ContainsKey(roomObj))
                {
                    var wallsInRoom = roomMap[roomObj].Where(o => DetectComponentType(o) == HouseComponentType.Wall).OrderBy(w => w.name).ToList();
                    if (wallsInRoom.Count > 0)
                    {
                        sb.AppendLine($"// Walls in {roomObj.name}:");
                        foreach (GameObject wall in wallsInRoom) { sb.AppendLine(FormatAsWallSegment(wall, GetRoomFloorY(wall), 2.7f, 0.1f)); }
                    }

                    var doorsInRoom = roomMap[roomObj].Where(o => DetectComponentType(o) == HouseComponentType.Door).OrderBy(d => d.name).ToList();
                    if (doorsInRoom.Count > 0)
                    {
                        sb.AppendLine($"// Doors in {roomObj.name}:");
                        foreach (GameObject door in doorsInRoom) { sb.AppendLine(FormatAsDoorSpec(door)); }
                    }

                    var windowsInRoom = roomMap[roomObj].Where(o => DetectComponentType(o) == HouseComponentType.Window).OrderBy(w => w.name).ToList();
                    if (windowsInRoom.Count > 0)
                    {
                        sb.AppendLine($"// Windows in {roomObj.name}:");
                        foreach (GameObject window in windowsInRoom) { sb.AppendLine(FormatAsWindowSpec(window, GetRoomFloorY(window))); }
                    }
                }
                sb.AppendLine();
            }

            if (unassignedComponents.Count > 0)
            {
                sb.AppendLine("// --- Unassigned Components ---");
                var sortedUnassigned = unassignedComponents
                    .OrderBy(obj => DetectComponentType(obj).ToString())
                    .ThenBy(obj => obj.name);

                foreach (GameObject obj in sortedUnassigned)
                {
                    HouseComponentType componentType = DetectComponentType(obj);
                    switch (componentType)
                    {
                        case HouseComponentType.Room:
                            sb.AppendLine(FormatAsRoomData(obj));
                            break;
                        case HouseComponentType.Wall:
                            sb.AppendLine(FormatAsWallSegment(obj, GetRoomFloorY(obj), 2.7f, 0.1f));
                            break;
                        case HouseComponentType.Door:
                            sb.AppendLine(FormatAsDoorSpec(obj));
                            break;
                        case HouseComponentType.Window:
                            sb.AppendLine(FormatAsWindowSpec(obj, GetRoomFloorY(obj)));
                            break;
                        default:
                            sb.AppendLine($"// Detected {componentType} \"{obj.name}\" - Using generic transform output.");
                            sb.AppendLine(FormatGenericTransformData(obj));
                            break;
                    }
                }
            }
        }
        else
        {
            var sortedObjectsToProcess = objectsToProcess.Distinct()
                .OrderBy(obj => useContextualFormatting ? DetectComponentType(obj).ToString() : "")
                .ThenBy(obj => obj.name);

            foreach (GameObject obj in sortedObjectsToProcess)
            {
                if (useContextualFormatting)
                {
                    HouseComponentType componentType = DetectComponentType(obj);
                    switch (componentType)
                    {
                        case HouseComponentType.Room:
                            sb.AppendLine(FormatAsRoomData(obj));
                            break;
                        case HouseComponentType.Wall:
                            sb.AppendLine(FormatAsWallSegment(obj, GetRoomFloorY(obj), 2.7f, 0.1f));
                            break;
                        case HouseComponentType.Door:
                            sb.AppendLine(FormatAsDoorSpec(obj));
                            break;
                        case HouseComponentType.Window:
                            sb.AppendLine(FormatAsWindowSpec(obj, GetRoomFloorY(obj)));
                            break;
                        default:
                            sb.AppendLine($"// Detected {componentType} \"{obj.name}\" - Using generic transform output.");
                            sb.AppendLine(FormatGenericTransformData(obj));
                            break;
                    }
                }
                else
                {
                    sb.AppendLine(FormatGenericTransformData(obj));
                }
            }
        }

        generatedCode = sb.ToString();
        Repaint();
    }

    private string FormatGenericTransformData(GameObject obj)
    {
        StringBuilder sb = new StringBuilder();

        Vector3 worldPosition = obj.transform.position;
        Vector3 positionToOutput = worldPosition;
        string positionComment = " // Position (World Space)";

        switch (selectedCoordinateSpace)
        {
            case CoordinateSpaceSetting.RoomRelative:
                GameObject roomObject = GetRoomContext(obj);
                if (roomObject != null)
                {
                    Vector3 roomOrigin = GetRoomOrigin(obj);
                    positionToOutput = ConvertToRoomRelative(worldPosition, roomOrigin);
                    positionComment = $" // Position (Room Relative to '{roomObject.name}')";
                }
                else if (obj.transform.parent != null)
                {
                    positionToOutput = ConvertToRoomRelative(worldPosition, obj.transform.parent.position);
                    positionComment = $" // Position (Relative to parent '{obj.transform.parent.name}')";
                }
                break;

            case CoordinateSpaceSetting.WallRelative:
                if (obj.transform.parent != null)
                {
                    positionToOutput = ConvertToWallRelative(worldPosition, obj.transform.parent);
                    positionComment = $" // Position (Relative to parent '{obj.transform.parent.name}' as wall context)";
                }
                break;
        }

        sb.AppendLine($"// Transform data for \"{obj.name}\" (InstanceID: {obj.GetInstanceID()})");
        sb.AppendLine($"Vector3 position = new Vector3({positionToOutput.x.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString("f3", CultureInfo.InvariantCulture)}f);{positionComment}");
        Vector3 eulerAngles = obj.transform.eulerAngles;
        sb.AppendLine($"Quaternion rotation = Quaternion.Euler({eulerAngles.x.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.y.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.z.ToString("f1", CultureInfo.InvariantCulture)}f); // World rotation");
        Vector3 scale = obj.transform.localScale;
        sb.AppendLine($"Vector3 scale = new Vector3({scale.x.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.y.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.z.ToString("f3", CultureInfo.InvariantCulture)}f); // Local scale");
        sb.AppendLine();
        return sb.ToString();
    }

    private List<GameObject> FindAllHouseComponents(HouseComponentType? typeFilter = null)
    {
        List<GameObject> foundComponents = new List<GameObject>();
        GameObject houseRoot = GameObject.Find("ProceduralHouse_Generated");

        List<GameObject> objectsToSearch = new List<GameObject>();

        if (houseRoot != null)
        {
            // Search only under ProceduralHouse_Generated
            CollectChildrenRecursive(houseRoot.transform, objectsToSearch);
        }
        else
        {
            // Search all root GameObjects and their children
            foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                CollectChildrenRecursive(rootObj.transform, objectsToSearch);
            }
        }

        foreach (GameObject obj in objectsToSearch)
        {
            HouseComponentType componentType = DetectComponentType(obj);
            if (typeFilter == null) // No filter, add all detected house components (excluding Unknown unless specified)
            {
                if (componentType != HouseComponentType.Unknown) // Typically, we don't want 'Unknown' unless specifically asked for.
                {
                    foundComponents.Add(obj);
                }
            }
            // Handle single type filter
            else if (componentType == typeFilter.Value)
            {
                foundComponents.Add(obj);
            }
            // Special case for DoorsAndWindowsOnly where typeFilter might not directly support multiple values.
            // This will be handled in the calling logic in CaptureTransforms() for now,
            // or this method could be extended to take a List<HouseComponentType>.
            // For now, a single typeFilter is assumed as per the plan step's direct implementation.
        }

        return foundComponents;
    }

    // Helper method to recursively collect all children
    private void CollectChildrenRecursive(Transform parent, List<GameObject> list)
    {
        list.Add(parent.gameObject); // Add parent itself
        foreach (Transform child in parent)
        {
            CollectChildrenRecursive(child, list);
        }
    }

    private HouseComponentType DetectComponentType(GameObject obj)
    {
        if (obj.name == "ProceduralHouse_Generated") return HouseComponentType.ProceduralHouseRoot;
        if (obj.name == "Foundation") return HouseComponentType.Foundation;
        if (obj.name.StartsWith("Roof_")) return HouseComponentType.Roof;
        if (obj.name.StartsWith("Wall_")) return HouseComponentType.Wall;
        if (obj.name.StartsWith("Door_")) return HouseComponentType.Door;
        if (obj.name.StartsWith("Window_")) return HouseComponentType.Window;

        if (obj.GetComponent<RoomIdentifier>() != null) return HouseComponentType.Room;
        if (obj.name.Contains("Room")) return HouseComponentType.Room;

        return HouseComponentType.Unknown;
    }

    private GameObject GetRoomContext(GameObject obj)
    {
        if (obj == null) return null;

        // Check if the object itself is a room
        if (DetectComponentType(obj) == HouseComponentType.Room)
        {
            return obj;
        }

        // Traverse up the hierarchy
        Transform currentParent = obj.transform.parent;
        while (currentParent != null)
        {
            if (DetectComponentType(currentParent.gameObject) == HouseComponentType.Room)
            {
                return currentParent.gameObject;
            }
            currentParent = currentParent.parent;
        }

        return null; // No room context found
    }

    private Vector3 GetRoomOrigin(GameObject objInRoom)
    {
        GameObject roomObject = GetRoomContext(objInRoom);
        if (roomObject != null)
        {
            // Assuming the room's GameObject position is the desired origin.
            // If specific bounds (e.g., South-West corner) are needed, this logic would
            // need to be more complex, potentially involving Mesh Renderers or Colliders.
            return roomObject.transform.position;
        }
        else
        {
            Debug.LogWarning($"No room context found for '{objInRoom.name}'. Defaulting room origin to Vector3.zero.");
            return Vector3.zero;
        }
    }

    private Vector3 ConvertToRoomRelative(Vector3 worldPosition, Vector3 roomWorldOrigin)
    {
        return worldPosition - roomWorldOrigin;
    }

    private Vector3 ConvertToWallRelative(Vector3 worldPosition, Transform wallSegmentRootTransform)
    {
        if (wallSegmentRootTransform == null)
        {
            Debug.LogWarning("Attempted to convert to wall relative space with a null wallSegmentRootTransform. Returning world position.");
            return worldPosition; // Or handle as an error, e.g., return Vector3.zero or throw exception
        }
        return wallSegmentRootTransform.InverseTransformPoint(worldPosition);
    }

    private float GetRoomFloorY(GameObject obj)
    {
        GameObject roomObject = GetRoomContext(obj);

        if (roomObject != null)
        {
            Transform floorChild = null;
            for (int i = 0; i < roomObject.transform.childCount; i++)
            {
                Transform child = roomObject.transform.GetChild(i);
                if (child.name.Equals("Floor", System.StringComparison.OrdinalIgnoreCase))
                {
                    floorChild = child;
                    break;
                }
            }

            if (floorChild != null)
            {
                MeshRenderer floorRenderer = floorChild.GetComponent<MeshRenderer>();
                if (floorRenderer != null)
                {
                    return floorRenderer.bounds.min.y;
                }
                else
                {
                    Debug.LogWarning($"GetRoomFloorY: Floor child named '{floorChild.name}' found for room '{roomObject.name}', but it has no MeshRenderer. Using room's Y position as fallback.");
                    return roomObject.transform.position.y;
                }
            }
            else
            {
                // No child explicitly named "Floor", use the room's main Y position.
                return roomObject.transform.position.y;
            }
        }
        else
        {
            Debug.LogWarning($"GetRoomFloorY: Could not determine room context for object '{(obj != null ? obj.name : "null")}'. Defaulting roomFloorY to 0.0f.");
            return 0.0f;
        }
    }

    private string GetProcessedPositionString(GameObject obj, Vector3 worldPosition, string componentTypeName)
    {
        Vector3 positionToOutput = worldPosition;
        string positionComment = ""; // To add context like "(World Space)" or "(Room Relative)"

        switch (selectedCoordinateSpace)
        {
            case CoordinateSpaceSetting.RoomRelative:
                GameObject roomObject = GetRoomContext(obj);
                if (roomObject != null)
                {
                    Vector3 roomOrigin = GetRoomOrigin(obj); // obj or roomObject should both work if obj is inside
                    positionToOutput = ConvertToRoomRelative(worldPosition, roomOrigin);
                    positionComment = $" // {componentTypeName} Position (Room Relative to '{roomObject.name}')";
                }
                else
                {
                    Debug.LogWarning($"'{obj.name}' ({componentTypeName}): RoomRelative selected, but no room context found. Defaulting to World Space.");
                    positionComment = $" // {componentTypeName} Position (World Space - No Room Context Found)";
                }
                break;

            case CoordinateSpaceSetting.WallRelative:
                Transform parentWallTransform = null;
                if (obj.transform.parent != null)
                {
                    // Assuming the direct parent is the wall.
                    // More robust wall finding might be needed if hierarchy is deeper/complex.
                    if (DetectComponentType(obj.transform.parent.gameObject) == HouseComponentType.Wall)
                    {
                        parentWallTransform = obj.transform.parent;
                    }
                }

                if (parentWallTransform != null)
                {
                    positionToOutput = ConvertToWallRelative(worldPosition, parentWallTransform);
                    positionComment = $" // {componentTypeName} Position (Wall Relative to '{parentWallTransform.name}')";
                }
                else
                {
                    Debug.LogWarning($"'{obj.name}' ({componentTypeName}): WallRelative selected, but no parent wall found or parent is not a Wall. Defaulting to World Space.");
                    positionComment = $" // {componentTypeName} Position (World Space - No Parent Wall Found)";
                }
                break;

            case CoordinateSpaceSetting.World:
            default:
                positionComment = $" // {componentTypeName} Position (World Space)";
                break;
        }
        return $"Vector3 position = new Vector3({positionToOutput.x.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString("f3", CultureInfo.InvariantCulture)}f);{positionComment}";
    }

    private string FormatAsRoomData(GameObject roomObject) // Changed parameter name for clarity
    {
        StringBuilder sb = new StringBuilder();

        // Derive roomId and roomLabel from roomObject.name
        string roomId = roomObject.name; // Assuming name is unique and suitable for ID
        string roomLabel = roomObject.name; // Can be the same as ID or processed further if needed

        // Calculate dimensions
        Vector2 dimensions;
        MeshRenderer meshRenderer = roomObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null && meshRenderer.bounds.size != Vector3.zero)
        {
            dimensions = new Vector2(meshRenderer.bounds.size.x, meshRenderer.bounds.size.z);
        }
        else
        {
            dimensions = new Vector2(3f, 3f); // Placeholder dimensions
            Debug.LogWarning($"Room '{roomObject.name}': MeshRenderer not found or bounds are zero. Using placeholder dimensions ({dimensions.x.ToString("f3", CultureInfo.InvariantCulture)}f, {dimensions.y.ToString("f3", CultureInfo.InvariantCulture)}f).");
        }

        // Use roomObject.transform.position for position
        Vector3 position = roomObject.transform.position;

        // Initialize walls, connectedRoomIds, notes, and atticHatchLocalPosition

        sb.AppendLine($"// RoomData for \"{roomObject.name}\" (InstanceID: {roomObject.GetInstanceID()})");
        sb.AppendLine("new RoomData");
        sb.AppendLine("{");
        sb.AppendLine($"    roomId = \"{roomId}\",");
        sb.AppendLine($"    roomLabel = \"{roomLabel}\",");
        sb.AppendLine($"    dimensions = new Vector2({dimensions.x.ToString("f3", CultureInfo.InvariantCulture)}f, {dimensions.y.ToString("f3", CultureInfo.InvariantCulture)}f),");
        sb.AppendLine($"    position = new Vector3({position.x.ToString("f3", CultureInfo.InvariantCulture)}f, {position.y.ToString("f3", CultureInfo.InvariantCulture)}f, {position.z.ToString("f3", CultureInfo.InvariantCulture)}f), // World Position");
        sb.AppendLine("    walls = new List<WallSegment>(), // Placeholder for actual wall data");
        sb.AppendLine("    connectedRoomIds = new List<string>(), // Placeholder for actual connected room IDs");
        sb.AppendLine("    notes = \"\",");
        sb.AppendLine($"    atticHatchLocalPosition = new Vector3({Vector3.zero.x.ToString("f3", CultureInfo.InvariantCulture)}f, {Vector3.zero.y.ToString("f3", CultureInfo.InvariantCulture)}f, {Vector3.zero.z.ToString("f3", CultureInfo.InvariantCulture)}f)");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    private string FormatAsWallSegment(GameObject wallRootObject, float roomFloorY, float storyHeight, float wallThickness)
    {
        StringBuilder sb = new StringBuilder();

        // Call WallSegmentAnalyzer.AnalyzeWallGeometry
        // Ensure WallSegmentAnalyzer and AnalyzedWallData are defined and accessible
        WallSegmentAnalyzer.AnalyzedWallData analyzedData = WallSegmentAnalyzer.AnalyzeWallGeometry(wallRootObject, roomFloorY, storyHeight, wallThickness);

        // Set startPoint to wallRootObject.transform.position
        Vector3 startPoint = wallRootObject.transform.position;

        // Calculate endPoint using analyzedData.localEndPoint transformed to world space
        Vector3 endPoint = wallRootObject.transform.TransformPoint(analyzedData.localEndPoint);

        // Use wallThickness parameter for the thickness field.
        // The requirement is to use the wallThickness parameter, which was also passed to AnalyzeWallGeometry.
        // If AnalyzeWallGeometry could modify it and return it as determinedThickness, that could also be an option.
        // For now, sticking to the passed-in wallThickness for the WallSegment's thickness.
        float currentThickness = wallThickness;

        // Use analyzedData.isLikelyExterior for isExterior
        bool isExterior = analyzedData.isLikelyExterior;

        // Initialize doorIdsOnWall, windowIdsOnWall, and openingIdsOnWall
        // These would be populated if child objects representing doors/windows were analyzed,
        // which is beyond the current scope.

        sb.AppendLine($"// WallSegment for \"{wallRootObject.name}\" (InstanceID: {wallRootObject.GetInstanceID()})");
        sb.AppendLine("new WallSegment");
        sb.AppendLine("{");
        sb.AppendLine($"    startPoint = new Vector3({startPoint.x.ToString("f3", CultureInfo.InvariantCulture)}f, {startPoint.y.ToString("f3", CultureInfo.InvariantCulture)}f, {startPoint.z.ToString("f3", CultureInfo.InvariantCulture)}f), // World Space");
        sb.AppendLine($"    endPoint = new Vector3({endPoint.x.ToString("f3", CultureInfo.InvariantCulture)}f, {endPoint.y.ToString("f3", CultureInfo.InvariantCulture)}f, {endPoint.z.ToString("f3", CultureInfo.InvariantCulture)}f), // World Space");
        sb.AppendLine($"    thickness = {currentThickness.ToString("f3", CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    isExterior = {isExterior.ToString().ToLowerInvariant()},"); // Format bool as lowercase true/false
        sb.AppendLine("    doorIdsOnWall = new List<string>(), // Placeholder for actual door IDs");
        sb.AppendLine("    windowIdsOnWall = new List<string>(), // Placeholder for actual window IDs");
        sb.AppendLine("    openingIdsOnWall = new List<string>() // Placeholder for actual opening IDs");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    private string FormatAsDoorSpec(GameObject doorObject) // Changed param name
    {
        StringBuilder sb = new StringBuilder();

        string doorId = doorObject.name;

        // Infer Type - Assuming DoorType enum exists (e.g., DoorType.Hinged, DoorType.Sliding)
        DoorType type = DoorType.Hinged; // Default
        if (doorObject.name.IndexOf("Sliding", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            type = DoorType.Sliding;
        }

        // Get Width and Height from Renderer bounds
        float width = 0.8f; // Default placeholder
        float height = 2.0f; // Default placeholder
        Renderer renderer = doorObject.GetComponent<Renderer>();
        if (renderer != null && renderer.bounds.size != Vector3.zero)
        {
            width = renderer.bounds.size.x;
            height = renderer.bounds.size.y; // Assuming Y is height for a door
        }
        else
        {
            Debug.LogWarning($"Door '{doorObject.name}': Renderer not found or bounds are zero. Using placeholder dimensions (Width: {width.ToString("f3", CultureInfo.InvariantCulture)}f, Height: {height.ToString("f3", CultureInfo.InvariantCulture)}f).");
        }

        // Position - Replicating GetProcessedPositionString logic for correct formatting here
        Vector3 worldPos = doorObject.transform.position;
        Vector3 positionToOutput = worldPos;
        string positionComment = " // Position (World Space)"; // Default

        switch (selectedCoordinateSpace)
        {
            case CoordinateSpaceSetting.RoomRelative:
                GameObject roomCtx = GetRoomContext(doorObject);
                if (roomCtx != null) {
                    positionToOutput = ConvertToRoomRelative(worldPos, GetRoomOrigin(doorObject)); // GetRoomOrigin uses GetRoomContext
                    positionComment = $" // Door Position (Room Relative to '{roomCtx.name}')";
                } else {
                     // No room context, fallback to parent if available, else world
                    if (doorObject.transform.parent != null) {
                        positionToOutput = ConvertToRoomRelative(worldPos, doorObject.transform.parent.position);
                        positionComment = $" // Door Position (Relative to parent '{doorObject.transform.parent.name}')";
                        Debug.LogWarning($"'{doorObject.name}' (Door): RoomRelative selected, no room context. Outputting relative to parent '{doorObject.transform.parent.name}'.");
                    } else {
                        positionComment = $" // Door Position (World Space - RoomRelative selected, but no Room Context or parent found)";
                        Debug.LogWarning($"'{doorObject.name}' (Door): RoomRelative selected, but no Room Context or parent found. Defaulting to World Space.");
                    }
                }
                break;
            case CoordinateSpaceSetting.WallRelative:
                Transform parentWall = null;
                if (doorObject.transform.parent != null) {
                    if (DetectComponentType(doorObject.transform.parent.gameObject) == HouseComponentType.Wall) {
                        parentWall = doorObject.transform.parent;
                    }
                }
                if (parentWall != null) {
                    positionToOutput = ConvertToWallRelative(worldPos, parentWall);
                    positionComment = $" // Door Position (Wall Relative to '{parentWall.name}')";
                } else {
                    positionComment = $" // Door Position (World Space - WallRelative selected, but no parent Wall found)";
                    Debug.LogWarning($"'{doorObject.name}' (Door): WallRelative selected, but no parent Wall found. Defaulting to World Space.");
                }
                break;
            case CoordinateSpaceSetting.World:
            default:
                // positionToOutput is already worldPos; positionComment is already set
                break;
        }
        string formattedPosition = $"new Vector3({positionToOutput.x.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString("f3", CultureInfo.InvariantCulture)}f)";

        // Wall ID
        string wallId = "UNKNOWN_WALL_ID";
        string wallIdComment = "// Parent Wall ID: None";
        if (doorObject.transform.parent != null)
        {
            if (DetectComponentType(doorObject.transform.parent.gameObject) == HouseComponentType.Wall)
            {
                wallId = doorObject.transform.parent.name;
                wallIdComment = $"// Parent Wall ID: {wallId}";
            }
            else
            {
                 Debug.LogWarning($"Door '{doorObject.name}': Parent '{doorObject.transform.parent.name}' is not detected as a Wall. Using placeholder wallId.");
                 wallIdComment = $"// Parent Wall ID: {doorObject.transform.parent.name} (Not a Wall)";
            }
        }
        else
        {
            Debug.LogWarning($"Door '{doorObject.name}': No parent found. Using placeholder wallId.");
        }

        // Default values for other properties - Assuming SwingDirection and SlideDirection enums exist
        SwingDirection swingDirection = SwingDirection.InwardNorth;
        SlideDirection slideDirection = SlideDirection.SlidesLeft;
        bool isExterior = false;
        string connectsRoomA_Id = "";
        string connectsRoomB_Id = "";

        sb.AppendLine($"// DoorSpec for \"{doorObject.name}\" (InstanceID: {doorObject.GetInstanceID()})");
        sb.AppendLine("new DoorSpec");
        sb.AppendLine("{");
        sb.AppendLine($"    doorId = \"{doorId}\",");
        sb.AppendLine($"    type = DoorType.{type.ToString()},"); // Assumes DoorType enum
        sb.AppendLine($"    width = {width.ToString("f3", CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    height = {height.ToString("f3", CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    position = {formattedPosition},{positionComment}");
        sb.AppendLine($"    wallId = \"{wallId}\", {wallIdComment}"); // NEW LINE
        sb.AppendLine($"    swingDirection = SwingDirection.{swingDirection.ToString()},"); // Assumes SwingDirection enum
        if (type == DoorType.Sliding)
        {
            sb.AppendLine($"    slideDirection = SlideDirection.{slideDirection.ToString()},"); // Assumes SlideDirection enum
        }
        sb.AppendLine($"    isExterior = {isExterior.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    connectsRoomA_Id = \"{connectsRoomA_Id}\",");
        sb.AppendLine($"    connectsRoomB_Id = \"{connectsRoomB_Id}\"");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    private string FormatAsWindowSpec(GameObject windowObject, float roomFloorY) // Added roomFloorY, changed param name
    {
        StringBuilder sb = new StringBuilder();

        string windowId = windowObject.name;

        // Infer Type - Defaulting as WindowPlaceholder is not confirmed to exist.
        // Assumes a WindowType enum (e.g. WindowType.SingleHung) is defined elsewhere.
        WindowType type = WindowType.SingleHung; // Default

        // Commented out WindowPlaceholder logic as the component presence is unconfirmed by ls()
        // WindowPlaceholder placeholder = windowObject.GetComponent<WindowPlaceholder>();
        // if (placeholder != null)
        // {
        //     // Assumes WindowPlaceholder.PlaceholderType enum values match string names of WindowType enum values
        //     if (System.Enum.TryParse(placeholder.placeholderType.ToString(), out WindowType parsedType))
        //     {
        //         type = parsedType;
        //     }
        //     else
        //     {
        //         Debug.LogWarning($"Window '{windowObject.name}': Could not parse WindowPlaceholder.PlaceholderType '{placeholder.placeholderType.ToString()}' to WindowType. Defaulting to {type}.");
        //     }
        // }

        // Get Width and Height from Renderer bounds
        float width = 1.0f; // Default placeholder
        float height = 1.2f; // Default placeholder
        Renderer renderer = windowObject.GetComponent<Renderer>();
        if (renderer != null && renderer.bounds.size != Vector3.zero)
        {
            width = renderer.bounds.size.x;
            height = renderer.bounds.size.y; // Assuming Y is height for a window
        }
        else
        {
            Debug.LogWarning($"Window '{windowObject.name}': Renderer not found or bounds are zero. Using placeholder dimensions (Width: {width.ToString("f3", CultureInfo.InvariantCulture)}f, Height: {height.ToString("f3", CultureInfo.InvariantCulture)}f).");
        }

        // Position - Adapting GetProcessedPositionString logic
        Vector3 worldPos = windowObject.transform.position;
        Vector3 positionToOutput = worldPos;
        string positionComment = " // Position (World Space)"; // Default

        switch (selectedCoordinateSpace)
        {
            case CoordinateSpaceSetting.RoomRelative:
                GameObject roomCtx = GetRoomContext(windowObject);
                if (roomCtx != null) {
                    positionToOutput = ConvertToRoomRelative(worldPos, GetRoomOrigin(windowObject));
                    positionComment = $" // Window Position (Room Relative to '{roomCtx.name}')";
                } else {
                    if (windowObject.transform.parent != null) {
                        positionToOutput = ConvertToRoomRelative(worldPos, windowObject.transform.parent.position);
                        positionComment = $" // Window Position (Relative to parent '{windowObject.transform.parent.name}')";
                        Debug.LogWarning($"'{windowObject.name}' (Window): RoomRelative selected, no room context. Outputting relative to parent '{windowObject.transform.parent.name}'.");
                    } else {
                        positionComment = $" // Window Position (World Space - RoomRelative selected, but no Room Context or parent found)";
                        Debug.LogWarning($"'{windowObject.name}' (Window): RoomRelative selected, but no Room Context or parent found. Defaulting to World Space.");
                    }
                }
                break;
            case CoordinateSpaceSetting.WallRelative:
                Transform parentWall = null;
                if (windowObject.transform.parent != null) {
                    if (DetectComponentType(windowObject.transform.parent.gameObject) == HouseComponentType.Wall) {
                        parentWall = windowObject.transform.parent;
                    }
                }
                if (parentWall != null) {
                    positionToOutput = ConvertToWallRelative(worldPos, parentWall);
                    positionComment = $" // Window Position (Wall Relative to '{parentWall.name}')";
                } else {
                    positionComment = $" // Window Position (World Space - WallRelative selected, but no parent Wall found)";
                    Debug.LogWarning($"'{windowObject.name}' (Window): WallRelative selected, but no parent Wall found. Defaulting to World Space.");
                }
                break;
            case CoordinateSpaceSetting.World:
            default:
                // positionToOutput is already worldPos; positionComment is already set
                break;
        }
        string formattedPosition = $"new Vector3({positionToOutput.x.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString("f3", CultureInfo.InvariantCulture)}f)";

        // Sill Height
        float sillHeight = windowObject.transform.position.y - roomFloorY;

        // Wall ID
        string wallId = "UNKNOWN_WALL_ID";
        string wallIdComment = "// Parent Wall ID: None";
        if (windowObject.transform.parent != null)
        {
            if (DetectComponentType(windowObject.transform.parent.gameObject) == HouseComponentType.Wall)
            {
                wallId = windowObject.transform.parent.name;
                wallIdComment = $"// Parent Wall ID: {wallId}";
            }
            else
            {
                 Debug.LogWarning($"Window '{windowObject.name}': Parent '{windowObject.transform.parent.name}' is not detected as a Wall. Using placeholder wallId.");
                 wallIdComment = $"// Parent Wall ID: {windowObject.transform.parent.name} (Not a Wall)";
            }
        }
        else
        {
            Debug.LogWarning($"Window '{windowObject.name}': No parent found. Using placeholder wallId.");
        }

        // Default values for other properties
        bool isOperable = true;
        int bayPanes = 0;
        float bayProjectionDepth = 0.0f;

        sb.AppendLine($"// WindowSpec for \"{windowObject.name}\" (InstanceID: {windowObject.GetInstanceID()})");
        sb.AppendLine("new WindowSpec");
        sb.AppendLine("{");
        sb.AppendLine($"    windowId = \"{windowId}\",");
        sb.AppendLine($"    type = WindowType.{type.ToString()},"); // Assumes WindowType enum
        sb.AppendLine($"    width = {width.ToString("f3", CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    height = {height.ToString("f3", CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    position = {formattedPosition},{positionComment}");
        sb.AppendLine($"    sillHeight = {sillHeight.ToString("f3", CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    wallId = \"{wallId}\", {wallIdComment}"); // NEW LINE
        sb.AppendLine($"    isOperable = {isOperable.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    bayPanes = {bayPanes.ToString(CultureInfo.InvariantCulture)},"); // int, no "f"
        sb.AppendLine($"    bayProjectionDepth = {bayProjectionDepth.ToString("f3", CultureInfo.InvariantCulture)}f");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }
}
