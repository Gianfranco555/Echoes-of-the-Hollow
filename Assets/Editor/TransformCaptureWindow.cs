using UnityEditor;
using UnityEngine;
// Placeholder enums (DoorType, SwingDirection, SlideDirection, WindowType) removed.
// These should now be referenced from HousePlanSO if needed, e.g. global::DoorType
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
    private string housePlanAssetPath = "Assets/BlueprintData/NewHousePlan.asset"; // Added for plan comparison
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

        EditorGUILayout.Space(); // Added for layout
        housePlanAssetPath = EditorGUILayout.TextField("House Plan Asset Path", housePlanAssetPath);
        if (GUILayout.Button("Compare Captured with Current Plan"))
        {
            CompareWithPlan();
        }
        EditorGUILayout.Space(); // Added for layout

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
        // Assumes a WindowType enum (e.g. global::WindowType.SingleHung) is defined elsewhere.
        global::WindowType type = global::WindowType.SingleHung; // Default

        // Commented out WindowPlaceholder logic as the component presence is unconfirmed by ls()
        // WindowPlaceholder placeholder = windowObject.GetComponent<WindowPlaceholder>();
        // if (placeholder != null)
        // {
        //     // Assumes WindowPlaceholder.PlaceholderType enum values match string names of WindowType enum values
        //     if (System.Enum.TryParse(placeholder.placeholderType.ToString(), out global::WindowType parsedType))
        //     {
        //         type = parsedType;
        //     }
        //     else
        //     {
        //         Debug.LogWarning($"Window '{windowObject.name}': Could not parse WindowPlaceholder.PlaceholderType '{placeholder.placeholderType.ToString()}' to global::WindowType. Defaulting to {type}.");
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
        sb.AppendLine($"    type = global::WindowType.{type.ToString()},"); // Assumes WindowType enum from HousePlanSO
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

    // Stub methods for the new comparison functionality
    private void CompareWithPlan()
    {
        generatedCode = "Starting comparison...\n";
        HousePlanSO loadedPlan = HousePlanDiffer.LoadTargetHousePlan(housePlanAssetPath);

        if (loadedPlan == null)
        {
            generatedCode += "<color=red>Error: Could not load the House Plan SO from path: " + housePlanAssetPath + ". Check path and console.</color>";
            EditorUtility.DisplayDialog("Error", "Failed to load HousePlanSO. Check asset path and console for details.", "OK");
            return;
        }
        generatedCode += $"Loaded plan '{loadedPlan.name}' successfully from '{housePlanAssetPath}'.\nCapturing scene data...\n";
        Repaint(); // Update UI

        var capturedData = CaptureSceneDataAsStructs(loadedPlan);
        generatedCode += $"Scene data capture attempt finished. Rooms: {capturedData.rooms.Count}, Doors: {capturedData.doors.Count}, Windows: {capturedData.windows.Count}, Openings: {capturedData.openings.Count}\n";
        Repaint(); // Update UI

        generatedCode += "Performing comparison with HousePlanDiffer...\n";
        Repaint(); // Update UI

        DiffResultSet diffResult = HousePlanDiffer.ComparePlanToScene(
            loadedPlan,
            capturedData.rooms,
            capturedData.doors,
            capturedData.windows,
            capturedData.openings
        );

        if (diffResult == null)
        {
            generatedCode += "<color=red>Error: Comparison failed. HousePlanDiffer.ComparePlanToScene returned null.</color>";
            EditorUtility.DisplayDialog("Error", "Comparison failed. DiffResultSet is null. Check console for errors from HousePlanDiffer.", "OK");
            return;
        }

        DisplayDiffResults(diffResult);
        generatedCode += "\nComparison complete.";
        Repaint(); // Update UI
    }

    private (List<RoomData> rooms, List<DoorSpec> doors, List<WindowSpec> windows, List<OpeningSpec> openings) CaptureSceneDataAsStructs(HousePlanSO existingPlanForContext)
    {
        generatedCode += "\nStarting scene data capture...\n";
        List<RoomData> capturedRooms = new List<RoomData>();
        List<DoorSpec> capturedDoors = new List<DoorSpec>();
        List<WindowSpec> capturedWindows = new List<WindowSpec>();
        List<OpeningSpec> capturedOpenings = new List<OpeningSpec>();

        // Use existing FindAllHouseComponents to get all relevant GameObjects.
        // This method might need refinement if it doesn't find all desired objects or finds too many.
        List<GameObject> allGameObjects = FindAllHouseComponents();

        // Default values from existing plan context if available
        float storyHeight = existingPlanForContext?.storyHeight ?? 2.7f;
        float defaultWallThickness = existingPlanForContext?.exteriorWallThickness ?? 0.15f; // Default, might need interior too

        foreach (GameObject go in allGameObjects)
        {
            HouseComponentType componentType = DetectComponentType(go);

            switch (componentType)
            {
                case HouseComponentType.Room:
                    RoomData roomData = new RoomData();
                    roomData.roomId = go.name; // Use GameObject name as ID. Consider a more robust ID system.
                    roomData.roomLabel = go.name; // Label can be same as ID or a "cleaner" version.

                    Renderer roomRenderer = go.GetComponent<Renderer>();
                    if (roomRenderer != null) {
                        roomData.dimensions = new Vector2(roomRenderer.bounds.size.x, roomRenderer.bounds.size.z);
                        roomData.position = new Vector3(roomRenderer.bounds.center.x, GetRoomFloorY(go), roomRenderer.bounds.center.z - roomRenderer.bounds.extents.z); // Assuming center pivot, adjust to corner
                    } else {
                        roomData.dimensions = new Vector2(1,1); // Default if no renderer
                        roomData.position = go.transform.position; // Fallback to transform position
                        Debug.LogWarning($"Room '{go.name}' has no Renderer. Using default dimensions and transform position.");
                    }

                    roomData.notes = ""; // Scene capture typically doesn't include notes.
                    roomData.connectedRoomIds = new List<string>(); // Connection logic is complex, not part of basic capture.
                    roomData.atticHatchLocalPosition = Vector3.zero; // Default, specific capture needed if required.
                    roomData.walls = new List<WallSegment>();

                    float roomFloorY = GetRoomFloorY(go);

                    // Capture Walls for this Room
                    foreach (Transform childTransform in go.transform)
                    {
                        if (DetectComponentType(childTransform.gameObject) == HouseComponentType.Wall)
                        {
                            GameObject wallGO = childTransform.gameObject;
                            WallSegmentAnalyzer.AnalyzedWallData analyzedWall = WallSegmentAnalyzer.AnalyzeWallGeometry(
                                wallGO,
                                roomFloorY,
                                storyHeight,
                                defaultWallThickness // Pass a sensible default or context-based thickness
                            );

                            WallSegment wallSeg = new WallSegment();
                            // WallSegment ID/Key: For diffing, WallSegment needs a stable identifier.
                            // Using its world start/end points (rounded) is done in HousePlanDiffer.
                            // Here, we just populate the data.

                            // Approximating start/end points based on wall's transform and analyzed length.
                            // This assumes wallGO.transform.position is the center of the wall.
                            // And wallGO.transform.right is the direction of its length.
                            Vector3 center = wallGO.transform.position;
                            Vector3 halfLengthDir = wallGO.transform.right * analyzedWall.wallLength / 2f;
                            wallSeg.startPoint = center - halfLengthDir;
                            wallSeg.endPoint = center + halfLengthDir;

                            wallSeg.thickness = analyzedWall.determinedThickness;
                            wallSeg.isExterior = analyzedWall.isLikelyExterior;
                            wallSeg.side = WallSide.North; // Default. Inferring side is complex.

                            wallSeg.doorIdsOnWall = new List<string>();
                            wallSeg.windowIdsOnWall = new List<string>();
                            wallSeg.openingIdsOnWall = new List<string>();

                            // Capture items on this wall (Doors, Windows, Openings from WallSegmentAnalyzer)
                            foreach (Transform itemOnWallTransform in wallGO.transform)
                            {
                                GameObject itemGO = itemOnWallTransform.gameObject;
                                ComponentType itemType = DetectComponentType(itemGO);
                                string itemId = itemGO.name; // Using name as ID.

                                if (itemType == ComponentType.Door) wallSeg.doorIdsOnWall.Add(itemId);
                                else if (itemType == ComponentType.Window) wallSeg.windowIdsOnWall.Add(itemId);
                                // Explicit "Opening" GameObjects as children of walls are less common than analyzed openings.
                                else if (itemType == ComponentType.Opening) wallSeg.openingIdsOnWall.Add(itemId);
                            }

                            // Convert WallSegmentAnalyzer.OpeningData to global OpeningSpec list
                            // And add their IDs to wallSeg.openingIdsOnWall
                            if (analyzedWall.openings != null)
                            {
                                int openingIdx = 0;
                                foreach (var openingData in analyzedWall.openings)
                                {
                                    OpeningSpec os = new OpeningSpec();
                                    os.openingId = $"{wallGO.name}_AnalyzedOpening_{openingIdx++}"; // Generate unique ID

                                    // Map WallSegmentAnalyzer.OpeningTypeEnum to global::OpeningType
                                    switch(openingData.type) {
                                        case WallSegmentAnalyzer.OpeningTypeEnum.Doorway: os.type = global::OpeningType.CasedOpening; break; // Example mapping
                                        case WallSegmentAnalyzer.OpeningTypeEnum.Window: os.type = global::OpeningType.CasedOpening; break; // Or handle as window?
                                        case WallSegmentAnalyzer.OpeningTypeEnum.Passthrough: os.type = global::OpeningType.PassthroughCounter; break;
                                        default: os.type = global::OpeningType.CasedOpening; break;
                                    }
                                    os.width = openingData.size.x;
                                    os.height = openingData.size.y;
                                    // Position needs to be world space.
                                    os.position = wallGO.transform.TransformPoint(openingData.localPositionOnWall);
                                    os.wallId = wallGO.name; // Associate with this wall.
                                    // connectsRoomA/B_Id are hard to determine here.

                                    bool alreadyCaptured = capturedOpenings.Any(co => co.openingId == os.openingId);
                                    if(!alreadyCaptured) capturedOpenings.Add(os); // Add to global list

                                    if(!wallSeg.openingIdsOnWall.Contains(os.openingId)) wallSeg.openingIdsOnWall.Add(os.openingId);
                                }
                            }
                            roomData.walls.Add(wallSeg);
                        }
                    }
                    capturedRooms.Add(roomData);
                    break;

                case HouseComponentType.Door:
                    DoorSpec doorSpec = new DoorSpec();
                    doorSpec.doorId = go.name; // Use name as ID
                    doorSpec.position = go.transform.position;

                    Renderer doorRenderer = go.GetComponent<Renderer>();
                    doorSpec.width = doorRenderer != null ? doorRenderer.bounds.size.x : 0.8f;
                    doorSpec.height = doorRenderer != null ? doorRenderer.bounds.size.y : 2.0f;

                    // Initial type inference
                    if (go.name.ToLower().Contains("pocket")) doorSpec.type = global::DoorType.Pocket;
                    else if (go.name.ToLower().Contains("bifold")) doorSpec.type = global::DoorType.BiFold;
                    else if (go.name.ToLower().Contains("overhead")) doorSpec.type = global::DoorType.Overhead;
                    else if (go.name.ToLower().Contains("sliding")) doorSpec.type = global::DoorType.Sliding;
                    else doorSpec.type = global::DoorType.Hinged; // Default

                    // Check for SlidingDoorController component to override type to Sliding if present
                    var slidingController = go.GetComponent("SlidingDoorController");
                    if (slidingController != null) {
                        doorSpec.type = global::DoorType.Sliding;
                    }

                    // TODO: Implement robust inference for swingDirection and slideDirection.
                    // Currently using hardcoded defaults.
                    if (doorSpec.type == global::DoorType.Sliding) {
                        doorSpec.slideDirection = global::SlideDirection.SlidesLeft; // Default for sliding doors
                        doorSpec.swingDirection = global::SwingDirection.InwardNorth; // Or a "None" if available in HousePlanSO.SwingDirection
                    } else {
                        doorSpec.swingDirection = global::SwingDirection.InwardEast; // Default for hinged/other doors
                        doorSpec.slideDirection = global::SlideDirection.SlidesLeft; // Or a "None" if available in HousePlanSO.SlideDirection
                    }

                    doorSpec.isExterior = go.name.ToLower().Contains("exterior");

                    // Assign wallId based on parent, if parent is a wall
                    if (go.transform.parent != null) {
                        GameObject parentObject = go.transform.parent.gameObject;
                        if (DetectComponentType(parentObject) == HouseComponentType.Wall) {
                            doorSpec.wallId = parentObject.name;
                        } else {
                            doorSpec.wallId = "UNASSIGNED_OR_PARENT_NOT_WALL";
                        }
                    } else {
                        doorSpec.wallId = "NO_PARENT";
                    }

                    // doorSpec.connectsRoomA_Id / B_Id - hard to determine from this capture method
                    capturedDoors.Add(doorSpec);
                    break;

                case HouseComponentType.Window:
                    WindowSpec windowSpec = new WindowSpec();
                    windowSpec.windowId = go.name; // Use name as ID
                    windowSpec.position = go.transform.position;

                    Renderer windowRenderer = go.GetComponent<Renderer>();
                    windowSpec.width = windowRenderer != null ? windowRenderer.bounds.size.x : 1.2f;
                    windowSpec.height = windowRenderer != null ? windowRenderer.bounds.size.y : 1.0f;

                    float parentRoomFloorY = 0f; // Default to 0
                    bool floorYFound = false;

                    // Attempt 1: Get floor Y via room context (either window's room or parent wall's room)
                    GameObject roomForFloorContext = null;
                    if (go.transform.parent != null) {
                        GameObject parentObj = go.transform.parent.gameObject;
                        if (DetectComponentType(parentObj) == HouseComponentType.Wall) {
                            // Wall's parent should be the room
                            if (parentObj.transform.parent != null) {
                                roomForFloorContext = parentObj.transform.parent.gameObject;
                            } else {
                                // Wall has no parent, try window's room context directly
                                roomForFloorContext = GetRoomContext(go);
                                if(roomForFloorContext != null) Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent wall '{parentObj.name}' has no parent room. Using window's direct room context '{roomForFloorContext.name}'.");
                                else Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent wall '{parentObj.name}' has no parent room, and window has no direct room context.");
                            }
                        } else {
                            // Parent is not a wall, try window's room context directly
                            roomForFloorContext = GetRoomContext(go);
                             if(roomForFloorContext != null) Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent '{parentObj.name}' is not a wall. Using window's direct room context '{roomForFloorContext.name}'.");
                             else Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent '{parentObj.name}' is not a wall, and window has no direct room context.");
                        }
                    } else {
                        // Window has no parent, try window's room context directly
                        roomForFloorContext = GetRoomContext(go);
                        if(roomForFloorContext != null) Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}' has no parent. Using window's direct room context '{roomForFloorContext.name}'.");
                        else Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}' has no parent and no direct room context.");
                    }

                    if (roomForFloorContext != null && DetectComponentType(roomForFloorContext) == HouseComponentType.Room) {
                        parentRoomFloorY = GetRoomFloorY(roomForFloorContext);
                        floorYFound = true;
                        Debug.Log($"TransformCaptureWindow: Window '{go.name}' using floor Y {parentRoomFloorY} from room context '{roomForFloorContext.name}'.");
                    }

                    if (!floorYFound) {
                        Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}' could not determine floor Y from room context. Attempting raycast.");
                        float raycastDistance = (existingPlanForContext?.storyHeight ?? 2.7f) * 1.5f;
                        RaycastHit hitInfo;
                        if (Physics.Raycast(go.transform.position, Vector3.down, out hitInfo, raycastDistance)) {
                            if (hitInfo.collider.CompareTag("Floor")) {
                                parentRoomFloorY = hitInfo.point.y;
                                floorYFound = true;
                                Debug.Log($"TransformCaptureWindow: Window '{go.name}' found floor via raycast at Y: {parentRoomFloorY} (object: {hitInfo.collider.name}).");
                            } else {
                                Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}' raycast hit '{hitInfo.collider.name}' but it was not tagged 'Floor'.");
                                // Optionally use hitInfo.point.y anyway if any hit is better than none, but be cautious.
                            }
                        } else {
                             Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}' raycast downwards found no floor within {raycastDistance}m.");
                        }
                    }

                    if (!floorYFound) {
                        parentRoomFloorY = 0f;
                        Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}' failed to find any floor reference. Sill height will be calculated relative to world origin Y=0.");
                    }

                    windowSpec.sillHeight = go.transform.position.y - parentRoomFloorY;

                    // Infer type from name
                    if (go.name.ToLower().Contains("bay")) windowSpec.type = global::WindowType.Bay;
                    else if (go.name.ToLower().Contains("sliding")) windowSpec.type = global::WindowType.Sliding;
                    else if (go.name.ToLower().Contains("skylight")) windowSpec.type = global::WindowType.SkylightQuad;
                    else windowSpec.type = global::WindowType.SingleHung; // Default

                    windowSpec.isOperable = true; // Default
                    // Assign wallId based on parent, if parent is a wall
                    if (go.transform.parent != null) {
                        GameObject parentObject = go.transform.parent.gameObject;
                        if (DetectComponentType(parentObject) == HouseComponentType.Wall) {
                            windowSpec.wallId = parentObject.name;
                        } else {
                            windowSpec.wallId = "UNASSIGNED_OR_PARENT_NOT_WALL";
                        }
                    } else {
                        windowSpec.wallId = "NO_PARENT";
                    }
                    capturedWindows.Add(windowSpec);
                    break;
            }
        }
        generatedCode += $"Finished scene data capture. Rooms: {capturedRooms.Count}, Doors: {capturedDoors.Count}, Windows: {capturedWindows.Count}, Openings: {capturedOpenings.Count}.\n";
        Repaint();
        return (capturedRooms, capturedDoors, capturedWindows, capturedOpenings);
    }

    private void DisplayDiffResults(DiffResultSet diffResultSet)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Comparison Results:</b>");

        // Basic formatter, can be expanded if specific fields are needed for summary.
        var formatData = new System.Func<object, string>(data => {
            if (data == null) return "N/A";
            if (data is RoomData rd) return $"Label: {rd.roomLabel}, Pos:{rd.position.ToString("F1")}, Dims:{rd.dimensions.ToString("F1")}";
            if (data is WallSegment ws) return $"Start:{ws.startPoint.ToString("F1")}, End:{ws.endPoint.ToString("F1")}, Thick:{ws.thickness.ToString("F2")}";
            if (data is DoorSpec ds) return $"Type:{ds.type}, Pos:{ds.position.ToString("F1")}, W:{ds.width.ToString("F2")}, H:{ds.height.ToString("F2")}";
            if (data is WindowSpec wd) return $"Type:{wd.type}, Pos:{wd.position.ToString("F1")}, W:{wd.width.ToString("F2")}, H:{wd.height.ToString("F2")}";
            if (data is OpeningSpec os) return $"Type:{os.type}, Pos:{os.position.ToString("F1")}, W:{os.width.ToString("F2")}, H:{os.height.ToString("F2")}";
            return data.ToString();
        });

        int changeCount = 0;

        if (diffResultSet.roomDiffs != null && diffResultSet.roomDiffs.Count > 0) {
            sb.AppendLine("\n<b>--- Rooms ---</b>");
            foreach (var entry in diffResultSet.roomDiffs.OrderBy(e => e.change).ThenBy(e => e.id)) {
                AppendDiffEntry(sb, "Room", entry, formatData);
                if(entry.change != ChangeType.Unchanged) changeCount++;
            }
        }
        if (diffResultSet.wallDiffs != null && diffResultSet.wallDiffs.Count > 0) {
            sb.AppendLine("\n<b>--- Walls ---</b>");
             foreach (var entry in diffResultSet.wallDiffs.OrderBy(e => e.change).ThenBy(e => e.id)) {
                AppendDiffEntry(sb, "Wall", entry, formatData);
                if(entry.change != ChangeType.Unchanged) changeCount++;
            }
        }
        if (diffResultSet.doorDiffs != null && diffResultSet.doorDiffs.Count > 0) {
            sb.AppendLine("\n<b>--- Doors ---</b>");
            foreach (var entry in diffResultSet.doorDiffs.OrderBy(e => e.change).ThenBy(e => e.id)) {
                AppendDiffEntry(sb, "Door", entry, formatData);
                if(entry.change != ChangeType.Unchanged) changeCount++;
            }
        }
        if (diffResultSet.windowDiffs != null && diffResultSet.windowDiffs.Count > 0) {
            sb.AppendLine("\n<b>--- Windows ---</b>");
            foreach (var entry in diffResultSet.windowDiffs.OrderBy(e => e.change).ThenBy(e => e.id)) {
                AppendDiffEntry(sb, "Window", entry, formatData);
                if(entry.change != ChangeType.Unchanged) changeCount++;
            }
        }
        if (diffResultSet.openingDiffs != null && diffResultSet.openingDiffs.Count > 0) {
            sb.AppendLine("\n<b>--- Openings ---</b>");
            foreach (var entry in diffResultSet.openingDiffs.OrderBy(e => e.change).ThenBy(e => e.id)) {
                AppendDiffEntry(sb, "Opening", entry, formatData);
                if(entry.change != ChangeType.Unchanged) changeCount++;
            }
        }

        if (changeCount == 0)
        {
            sb.AppendLine("\n<color=green>No differences found between the loaded plan and the captured scene data (based on current capture logic).</color>");
        }
        generatedCode = sb.ToString(); // Append to existing logs or replace, for now it replaces.
    }

    private void AppendDiffEntry<T>(StringBuilder sb, string typeLabel, DiffEntry<T> entry, System.Func<object, string> formatter)
    {
        switch (entry.change)
        {
            case ChangeType.Added:
                sb.AppendLine($"<color=green>ADDED [{typeLabel}] ID: {entry.id} - Captured: {formatter(entry.capturedData)}</color>");
                break;
            case ChangeType.Removed:
                sb.AppendLine($"<color=red>REMOVED [{typeLabel}] ID: {entry.id} - Existing: {formatter(entry.existingData)}</color>");
                break;
            case ChangeType.Modified:
                sb.AppendLine($"<color=yellow>MODIFIED [{typeLabel}] ID: {entry.id}</color>");
                if (entry.differences != null) {
                    foreach (var diff in entry.differences) { sb.AppendLine($"    - {diff}"); }
                }
                sb.AppendLine($"  <color=#FFA500>└─ Existing: {formatter(entry.existingData)}</color>"); // Orange
                sb.AppendLine($"  <color=#FFFF00>└─ Captured: {formatter(entry.capturedData)}</color>"); // Yellow
                break;
            case ChangeType.Unchanged:
                 // Optionally show unchanged items, can be very verbose.
                 // sb.AppendLine($"UNCHANGED [{typeLabel}] ID: {entry.id} - Data: {formatter(entry.existingData)}");
                break;
        }
    }
}
