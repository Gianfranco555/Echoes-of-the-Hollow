using UnityEditor;
using UnityEngine;
// Placeholder enums (DoorType, SwingDirection, SlideDirection, WindowType) removed.
// These should now be referenced from HousePlanSO if needed, e.g. global::DoorType
using System.Text;
using System.Globalization; // Add this line
using System.Collections.Generic; // For List<T>
using System.Linq; // For Linq operations
using System.IO; // Added for Path and Directory operations

/// <summary>
/// Editor window for capturing transform data of GameObjects related to house components.
/// Provides functionality to generate C# code snippets for selected or scene-wide objects,
/// format data in various coordinate spaces, and compare with/update a HousePlanSO ScriptableObject.
/// </summary>
public class TransformCaptureWindow : EditorWindow
{
    /// <summary>
    /// Defines the type of a house component.
    /// </summary>
    public enum HouseComponentType { Unknown, Room, Wall, Door, Window, Foundation, Roof, ProceduralHouseRoot }
    /// <summary>
    /// Specifies the coordinate space for outputting position data.
    /// </summary>
    public enum CoordinateSpaceSetting { World, RoomRelative, WallRelative }
    /// <summary>
    /// Defines the scope of GameObjects to capture.
    /// </summary>
    public enum CaptureMode
    {
        SelectedObjects,
        ActiveScene_AllHouseComponents,
        ActiveScene_RoomsOnly,
        ActiveScene_WallsOnly,
        ActiveScene_DoorsAndWindowsOnly
    }
    private CaptureMode captureMode = CaptureMode.SelectedObjects;

    /// <summary>
    /// Defines the action to take when updating a HousePlanSO asset.
    /// </summary>
    public enum UpdateAction { GenerateCodeOnly, PreviewAssetChanges, ApplyChangesToAsset }
    private UpdateAction updateAction = UpdateAction.GenerateCodeOnly;

    private string generatedCode = "";
    private Vector2 scrollPosition;
    private string housePlanAssetPath = "Assets/BlueprintData/NewHousePlan.asset"; // Added for plan comparison
    private CoordinateSpaceSetting selectedCoordinateSpace = CoordinateSpaceSetting.World;
    private string currentInfoMessage;
    private MessageType currentInfoMessageType;
    private bool useContextualFormatting = false; // Added field
    private bool groupByRoom = false;

    private static HashSet<GameObject> recentlySelectedForCapture = new HashSet<GameObject>();
    private static HashSet<GameObject> successfullyCapturedLastRun = new HashSet<GameObject>();
    private static HashSet<GameObject> objectsWithCaptureErrorsLastRun = new HashSet<GameObject>();
    private static bool showCaptureGizmos = false;

    private static Dictionary<int, GameObject> s_roomContextCache;
    private static Dictionary<int, HouseComponentType> s_componentTypeCache;

    private static List<string> s_captureHistory = new List<string>(10);
    private static int s_currentHistoryIndex = -1;

    /// <summary>
    /// Opens the Transform Capturer editor window.
    /// </summary>
    [MenuItem("House Tools/Transform Data Capturer")]
    public static void ShowWindow()
    {
        GetWindow<TransformCaptureWindow>("Transform Capturer");
    }

    void OnGUI()
    {
        // Display Info/Error Messages
        if (!string.IsNullOrEmpty(currentInfoMessage))
        {
            EditorGUILayout.HelpBox(currentInfoMessage, currentInfoMessageType);
        }
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Capture Configuration", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        selectedCoordinateSpace = (CoordinateSpaceSetting)EditorGUILayout.EnumPopup("Coordinate Space:", selectedCoordinateSpace);
        showCaptureGizmos = EditorGUILayout.Toggle("Show Capture Gizmos", showCaptureGizmos);
        if (EditorGUI.EndChangeCheck()) // Assuming this check is for gizmos, should be tied to it
        {
            SceneView.RepaintAll();
        }
        useContextualFormatting = EditorGUILayout.Toggle("Use Contextual House Formatting", useContextualFormatting);
        captureMode = (CaptureMode)EditorGUILayout.EnumPopup("Capture Mode:", captureMode);
        EditorGUI.BeginDisabledGroup(!useContextualFormatting);
        groupByRoom = EditorGUILayout.Toggle("Group by Room", groupByRoom);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Primary Action", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        if (GUILayout.Button("Capture Selected Transforms", GUILayout.Height(35))) // Made button larger
        {
            CaptureTransforms();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("House Plan ScriptableObject Interaction", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        updateAction = (UpdateAction)EditorGUILayout.EnumPopup("Update Action:", updateAction);
        EditorGUILayout.Space();
        housePlanAssetPath = EditorGUILayout.TextField("House Plan Asset Path", housePlanAssetPath);

        EditorGUI.BeginDisabledGroup(updateAction != UpdateAction.ApplyChangesToAsset);
        if (GUILayout.Button("Execute Update on Asset"))
        {
            ExecuteUpdateOnAsset();
        }
        EditorGUI.EndDisabledGroup();

        // Disable "Compare" if "Apply" is the mode, or if some other condition makes compare invalid.
        // For now, just disabling if ApplyChangesToAsset is chosen, as comparison is usually a pre-step.
        EditorGUI.BeginDisabledGroup(updateAction == UpdateAction.ApplyChangesToAsset);
        if (GUILayout.Button("Compare Captured with Current Plan"))
        {
            CompareWithPlan();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Generated C# Code:", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        // Make TextArea read-only by using a style with normal.textColor set to a non-editable look,
        // or by simply not providing a way to change `generatedCode` other than CaptureTransforms.
        // For actual read-only behavior, one might use EditorGUI.SelectableLabel.
        // However, TextArea is fine for typical editor script usage if modification isn't intended.
        EditorGUILayout.TextArea(generatedCode, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(); // Space after text area

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = s_captureHistory.Count > 0 && s_currentHistoryIndex > 0;
        if (GUILayout.Button("Previous Capture", GUILayout.MinWidth(120))) { ShowPreviousCapture(); }
        GUI.enabled = s_captureHistory.Count > 0 && s_currentHistoryIndex < s_captureHistory.Count - 1;
        if (GUILayout.Button("Next Capture", GUILayout.MinWidth(120))) { ShowNextCapture(); }
        GUI.enabled = true;
        if (s_captureHistory.Count > 0)
        {
            GUILayout.Label($"History: {s_currentHistoryIndex + 1}/{s_captureHistory.Count}", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Import Text to View", GUILayout.MinWidth(150))) { ImportTextToView(); }
        if (GUILayout.Button("Export Captured Text", GUILayout.MinWidth(150))) { ExportCapturedText(); }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        if (GUILayout.Button("Copy Output to Clipboard", GUILayout.Height(30)))
        {
            InternalCopyGeneratedCodeToClipboard();
        }
        EditorGUILayout.Space(); // Final space at the bottom
    }

    void OnEnable()
    {
        TransformCapturePreferences.LoadPreferences(); // Ensure static class is up-to-date

        selectedCoordinateSpace = TransformCapturePreferences.DefaultCoordinateSpaceSetting;
        useContextualFormatting = TransformCapturePreferences.DefaultUseContextualFormattingSetting;
        groupByRoom = TransformCapturePreferences.DefaultGroupByRoomSetting;
        // The float precision is used directly from TransformCapturePreferences.FloatPrecisionForOutputSetting where needed.

        // Initialize history related things
        if (s_captureHistory.Count > 0 && s_currentHistoryIndex == -1)
        {
            s_currentHistoryIndex = s_captureHistory.Count - 1;
        }
        else if (s_currentHistoryIndex >= s_captureHistory.Count && s_captureHistory.Count > 0)
        {
             s_currentHistoryIndex = s_captureHistory.Count - 1;
        } else if (s_captureHistory.Count == 0) {
             s_currentHistoryIndex = -1;
        }
    }

    private void ImportTextToView()
    {
        string path = EditorUtility.OpenFilePanel(
            "Import Text to View", // Title
            "",                    // Directory
            "cs;txt"               // Allowed extensions
        );

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                generatedCode = System.IO.File.ReadAllText(path);
                scrollPosition = Vector2.zero; // Reset scroll to see the new content
                currentInfoMessage = $"Text imported from {System.IO.Path.GetFileName(path)}";
                currentInfoMessageType = MessageType.Info;
                ShowNotification(new GUIContent(currentInfoMessage)); // Also show notification
                Debug.Log($"Transform Capture: Text imported from {path}");
                Repaint(); // Update the window to show imported text
            }
            catch (System.Exception ex)
            {
                currentInfoMessage = $"Import failed: {ex.Message}";
                currentInfoMessageType = MessageType.Error;
                ShowNotification(new GUIContent("Import failed. See console.")); // Keep notification
                Debug.LogError($"Transform Capture: Error importing text from {path}. Exception: {ex.Message}");
                EditorUtility.DisplayDialog("Import Error", $"Failed to import text: {ex.Message}", "OK");
            }
        }
    }

    private void ExportCapturedText()
    {
        if (string.IsNullOrEmpty(generatedCode))
        {
            ShowNotification(new GUIContent("No text to export."));
            EditorUtility.DisplayDialog("Export Text", "There is no captured text to export.", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel(
            "Export Captured Text", // Title
            "",                   // Directory
            "CapturedTransformData.cs", // Default file name
            "cs;txt"              // Allowed extensions (cs for C# code, txt for plain text)
        );

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                System.IO.File.WriteAllText(path, generatedCode);
                currentInfoMessage = $"Text exported to {System.IO.Path.GetFileName(path)}";
                currentInfoMessageType = MessageType.Info;
                ShowNotification(new GUIContent(currentInfoMessage)); // Also show notification for quick feedback
                Debug.Log($"Transform Capture: Text exported to {path}");
            }
            catch (System.Exception ex)
            {
                currentInfoMessage = $"Export failed: {ex.Message}";
                currentInfoMessageType = MessageType.Error;
                ShowNotification(new GUIContent("Export failed. See console.")); // Keep notification for consistency
                Debug.LogError($"Transform Capture: Error exporting text to {path}. Exception: {ex.Message}");
                EditorUtility.DisplayDialog("Export Error", $"Failed to export text: {ex.Message}", "OK");
            }
        }
    }

    private void ShowPreviousCapture()
    {
        if (s_currentHistoryIndex > 0)
        {
            s_currentHistoryIndex--;
            generatedCode = s_captureHistory[s_currentHistoryIndex];
            scrollPosition = Vector2.zero; // Reset scroll to see the new content
            Repaint(); // Update the window view
        }
    }

    private void ShowNextCapture()
    {
        if (s_currentHistoryIndex < s_captureHistory.Count - 1)
        {
            s_currentHistoryIndex++;
            generatedCode = s_captureHistory[s_currentHistoryIndex];
            scrollPosition = Vector2.zero; // Reset scroll
            Repaint(); // Update the window view
        }
    }

    [MenuItem("House Tools/Quick Capture Selected &House_Capture #&t")]
    public static void QuickCaptureSelected()
    {
        TransformCaptureWindow window = EditorWindow.GetWindow<TransformCaptureWindow>("Transform Capturer");
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("Quick Capture: No objects selected.");
            window.ShowNotification(new GUIContent("No objects selected for Quick Capture."));
            return;
        }
        window.captureMode = CaptureMode.SelectedObjects; // Set mode on the instance
        window.CaptureTransforms(); // Call instance method
        if (!string.IsNullOrEmpty(window.generatedCode))
        {
            EditorGUIUtility.systemCopyBuffer = window.generatedCode;
            Debug.Log("Quick capture of selected objects copied to clipboard.");
            window.ShowNotification(new GUIContent("Selected objects captured and copied!"));
        }
        else
        {
            Debug.LogWarning("Quick Capture: Generated code was empty.");
            window.ShowNotification(new GUIContent("Capture resulted in empty output."));
        }
    }

    private void InternalCopyGeneratedCodeToClipboard()
    {
        if (string.IsNullOrEmpty(generatedCode))
        {
            ShowNotification(new GUIContent("Nothing to copy."));
            return;
        }
        EditorGUIUtility.systemCopyBuffer = generatedCode;
        ShowNotification(new GUIContent("Output copied to clipboard!"));
        Debug.Log("Generated code copied to clipboard.");
    }

    [MenuItem("House Tools/Copy Output to Clipboard #&c")]
    public static void CopyOutputToClipboard()
    {
        TransformCaptureWindow window = EditorWindow.GetWindow<TransformCaptureWindow>(false); // Don't create if not open
        if (window != null)
        {
            window.InternalCopyGeneratedCodeToClipboard();
        }
        else
        {
            Debug.LogWarning("Copy Output: Transform Capturer window is not open. Cannot copy.");
        }
    }

    private void InitializeCaches()
    {
        s_roomContextCache = new Dictionary<int, GameObject>();
        s_componentTypeCache = new Dictionary<int, HouseComponentType>();
    }

    private void CaptureTransforms()
    {
        InitializeCaches();
        currentInfoMessage = null; // Clear previous messages
        successfullyCapturedLastRun.Clear();
        objectsWithCaptureErrorsLastRun.Clear();
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

        // For now, assume all processed objects are successful. Error handling can be added later.
        foreach (GameObject obj in objectsToProcess.Distinct())
        {
            successfullyCapturedLastRun.Add(obj);
        }
        // Ensure SceneView repaints if gizmos are on
        if (showCaptureGizmos) SceneView.RepaintAll();

        if (objectsToProcess.Count == 0)
        {
            string specificMessage = "No objects found for the current Capture Mode.";
            if (captureMode == CaptureMode.SelectedObjects)
            {
                specificMessage = "No objects are currently selected for capture.";
            }

            generatedCode = $"// {specificMessage}";
            currentInfoMessage = specificMessage;
            currentInfoMessageType = MessageType.Warning;
            Repaint(); // Repaint to show update and help box
            return; // Exit early
        }

        // 2. Output Logic
        try
        {
            EditorUtility.DisplayProgressBar("Capturing Transforms", "Preparing...", 0f);

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

            int totalStepsWhenGrouping = allRooms.Count + unassignedComponents.Count;
            int currentStepWhenGrouping = 0;

            foreach (GameObject roomObj in allRooms)
            {
                currentStepWhenGrouping++;
                EditorUtility.DisplayProgressBar("Capturing Transforms", $"Processing Room: {roomObj.name}", (float)currentStepWhenGrouping / totalStepsWhenGrouping);

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
                    currentStepWhenGrouping++;
                    EditorUtility.DisplayProgressBar("Capturing Transforms", $"Processing: {obj.name}", (float)currentStepWhenGrouping / totalStepsWhenGrouping);

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
        else // Not grouped by room
        {
            var sortedObjectsToProcess = objectsToProcess.Distinct()
                .OrderBy(obj => useContextualFormatting ? DetectComponentType(obj).ToString() : "")
                .ThenBy(obj => obj.name)
                .ToList(); // ToList to get a count for progress

            for (int i = 0; i < sortedObjectsToProcess.Count; i++)
            {
                GameObject obj = sortedObjectsToProcess[i];
                EditorUtility.DisplayProgressBar("Capturing Transforms", $"Processing: {obj.name}", (float)(i + 1) / sortedObjectsToProcess.Count);

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

        if (!string.IsNullOrEmpty(generatedCode) && !generatedCode.StartsWith("// No objects found")) // Avoid adding trivial/empty captures
        {
            // Remove future history if we went back and made a new capture
            if (s_currentHistoryIndex < s_captureHistory.Count - 1)
            {
                s_captureHistory.RemoveRange(s_currentHistoryIndex + 1, s_captureHistory.Count - (s_currentHistoryIndex + 1));
            }

            // Limit history size
            if (s_captureHistory.Count >= 10) // Max 10 items
            {
                s_captureHistory.RemoveAt(0); // Remove oldest
            }
            s_captureHistory.Add(generatedCode);
            s_currentHistoryIndex = s_captureHistory.Count - 1;
        }
        Repaint();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Set summary message after processing
        if (objectsToProcess.Count > 0) // Only show summary if we attempted processing
        {
            if (objectsWithCaptureErrorsLastRun.Count > 0)
            {
                currentInfoMessage = $"{objectsWithCaptureErrorsLastRun.Count} object(s) encountered issues during capture. See console/gizmos for details.";
                currentInfoMessageType = MessageType.Warning;
            }
            else
            {
                currentInfoMessage = "Capture successful.";
                currentInfoMessageType = MessageType.Info;
            }
            // Repaint is called at the end of the method, which will show this message.
        }
    }

    private string FormatGenericTransformData(GameObject obj)
    {
        StringBuilder sb = new StringBuilder();
        string formatString = "F" + TransformCapturePreferences.FloatPrecisionForOutputSetting;

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
        sb.AppendLine($"Vector3 position = new Vector3({positionToOutput.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString(formatString, CultureInfo.InvariantCulture)}f);{positionComment}");
        Vector3 eulerAngles = obj.transform.eulerAngles;
        sb.AppendLine($"Quaternion rotation = Quaternion.Euler({eulerAngles.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {eulerAngles.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {eulerAngles.z.ToString(formatString, CultureInfo.InvariantCulture)}f); // World rotation");
        Vector3 scale = obj.transform.localScale;
        sb.AppendLine($"Vector3 scale = new Vector3({scale.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {scale.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {scale.z.ToString(formatString, CultureInfo.InvariantCulture)}f); // Local scale");
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

    /// <summary>
    /// Detects the type of house component based on the GameObject's name or attached components.
    /// </summary>
    /// <param name="obj">The GameObject to analyze.</param>
    /// <returns>The detected HouseComponentType.</returns>
    private static HouseComponentType CalculateComponentType(GameObject obj)
    {
        if (obj.name.StartsWith("ProceduralHouse_Generated")) return HouseComponentType.ProceduralHouseRoot;
        if (obj.name.StartsWith("Foundation")) return HouseComponentType.Foundation;
        if (obj.name.StartsWith("Roof_")) return HouseComponentType.Roof;
        if (obj.name.StartsWith("Wall_")) return HouseComponentType.Wall;
        if (obj.name.StartsWith("Door_")) return HouseComponentType.Door;
        if (obj.name.StartsWith("Window_")) return HouseComponentType.Window;

        if (obj.GetComponent<RoomIdentifier>() != null) return HouseComponentType.Room;
        if (obj.name.Contains("Room")) return HouseComponentType.Room;

        return HouseComponentType.Unknown;
    }

    public static HouseComponentType DetectComponentType(GameObject obj)
    {
        if (obj == null) return HouseComponentType.Unknown;
        // Ensure cache is initialized before use, or handle null cache gracefully.
        if (s_componentTypeCache == null) {
            // This case should ideally not be hit if InitializeCaches() is called correctly.
            // Alternatively, could throw an error or initialize lazily.
            // For now, proceed to calculate if cache isn't ready, though this bypasses caching.
            return CalculateComponentType(obj);
        }
        int instanceID = obj.GetInstanceID();
        if (s_componentTypeCache.TryGetValue(instanceID, out HouseComponentType cachedType))
        {
            return cachedType;
        }
        HouseComponentType type = CalculateComponentType(obj); // Call the original logic
        s_componentTypeCache[instanceID] = type; // Add to cache
        return type;
    }

    private GameObject CalculateRoomContext(GameObject obj)
    {
        if (obj == null) return null;

        // Check if the object itself is a room
        if (DetectComponentType(obj) == HouseComponentType.Room) // This now calls the caching version
        {
            return obj;
        }

        // Traverse up the hierarchy
        Transform currentParent = obj.transform.parent;
        while (currentParent != null)
        {
            if (DetectComponentType(currentParent.gameObject) == HouseComponentType.Room) // This now calls the caching version
            {
                return currentParent.gameObject;
            }
            currentParent = currentParent.parent;
        }

        return null; // No room context found
    }

    private GameObject GetRoomContext(GameObject obj)
    {
        if (obj == null) return null;
        // Ensure cache is initialized
        if (s_roomContextCache == null) {
            // Similar to DetectComponentType, ideally not hit.
            return CalculateRoomContext(obj);
        }
        int instanceID = obj.GetInstanceID();
        if (s_roomContextCache.TryGetValue(instanceID, out GameObject cachedRoom))
        {
            return cachedRoom; // Can be null if that was the cached result
        }
        GameObject room = CalculateRoomContext(obj); // Call the original logic
        s_roomContextCache[instanceID] = room; // Add to cache
        return room;
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

    /// <summary>
    /// Converts a world position to a position relative to a room's world origin.
    /// </summary>
    /// <param name="worldPosition">The world position to convert.</param>
    /// <param name="roomWorldOrigin">The world origin of the room.</param>
    /// <returns>The position relative to the room's origin.</returns>
    public static Vector3 ConvertToRoomRelative(Vector3 worldPosition, Vector3 roomWorldOrigin)
    {
        return worldPosition - roomWorldOrigin;
    }

    /// <summary>
    /// Converts a world position to a position relative to a wall segment's root transform.
    /// </summary>
    /// <param name="worldPosition">The world position to convert.</param>
    /// <param name="wallSegmentRootTransform">The Transform of the wall segment's root.</param>
    /// <returns>The position relative to the wall segment's root transform.</returns>
    public static Vector3 ConvertToWallRelative(Vector3 worldPosition, Transform wallSegmentRootTransform)
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

    /// <summary>
    /// Formats the data of a given room GameObject into a C# string representation for a RoomData structure.
    /// </summary>
    /// <param name="roomObject">The GameObject representing the room.</param>
    /// <returns>A C# string snippet representing the room data.</returns>
    public string FormatAsRoomData(GameObject roomObject) // Changed parameter name for clarity
    {
        StringBuilder sb = new StringBuilder();
        string formatString = "F" + TransformCapturePreferences.FloatPrecisionForOutputSetting;

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
            Debug.LogWarning($"Room '{roomObject.name}': MeshRenderer not found or bounds are zero. Using placeholder dimensions ({dimensions.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {dimensions.y.ToString(formatString, CultureInfo.InvariantCulture)}f).");
        }

        // Use roomObject.transform.position for position
        Vector3 position = roomObject.transform.position;

        // Initialize walls, connectedRoomIds, notes, and atticHatchLocalPosition

        sb.AppendLine($"// RoomData for \"{roomObject.name}\" (InstanceID: {roomObject.GetInstanceID()})");
        sb.AppendLine("new RoomData");
        sb.AppendLine("{");
        sb.AppendLine($"    roomId = \"{roomId}\",");
        sb.AppendLine($"    roomLabel = \"{roomLabel}\",");
        sb.AppendLine($"    dimensions = new Vector2({dimensions.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {dimensions.y.ToString(formatString, CultureInfo.InvariantCulture)}f),");
        sb.AppendLine($"    position = new Vector3({position.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {position.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {position.z.ToString(formatString, CultureInfo.InvariantCulture)}f), // World Position");
        sb.AppendLine("    walls = new List<WallSegment>(), // Placeholder for actual wall data");
        sb.AppendLine("    connectedRoomIds = new List<string>(), // Placeholder for actual connected room IDs");
        sb.AppendLine("    notes = \"\",");
        sb.AppendLine($"    atticHatchLocalPosition = new Vector3({Vector3.zero.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {Vector3.zero.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {Vector3.zero.z.ToString(formatString, CultureInfo.InvariantCulture)}f)");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Formats the data of a given wall root GameObject into a C# string representation for a WallSegment structure.
    /// It uses WallSegmentAnalyzer.AnalyzeWallGeometry to get geometric details.
    /// </summary>
    /// <param name="wallRootObject">The GameObject representing the root of the wall segment.</param>
    /// <param name="roomFloorY">The Y coordinate of the room's floor, in world space.</param>
    /// <param name="storyHeight">The height of the story/room.</param>
    /// <param name="wallThickness">The thickness of the wall.</param>
    /// <returns>A C# string snippet representing the wall segment data.</returns>
    public string FormatAsWallSegment(GameObject wallRootObject, float roomFloorY, float storyHeight, float wallThickness)
    {
        StringBuilder sb = new StringBuilder();
        string formatString = "F" + TransformCapturePreferences.FloatPrecisionForOutputSetting;

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
        sb.AppendLine($"    startPoint = new Vector3({startPoint.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {startPoint.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {startPoint.z.ToString(formatString, CultureInfo.InvariantCulture)}f), // World Space");
        sb.AppendLine($"    endPoint = new Vector3({endPoint.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {endPoint.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {endPoint.z.ToString(formatString, CultureInfo.InvariantCulture)}f), // World Space");
        sb.AppendLine($"    thickness = {currentThickness.ToString(formatString, CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    isExterior = {isExterior.ToString().ToLowerInvariant()},"); // Format bool as lowercase true/false
        sb.AppendLine("    doorIdsOnWall = new List<string>(), // Placeholder for actual door IDs");
        sb.AppendLine("    windowIdsOnWall = new List<string>(), // Placeholder for actual window IDs");
        sb.AppendLine("    openingIdsOnWall = new List<string>() // Placeholder for actual opening IDs");
        sb.AppendLine("};");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Formats the data of a given door GameObject into a C# string representation for a DoorSpec structure.
    /// Handles coordinate space conversion based on the window's settings.
    /// </summary>
    /// <param name="doorObject">The GameObject representing the door.</param>
    /// <returns>A C# string snippet representing the door specification.</returns>
    public string FormatAsDoorSpec(GameObject doorObject) // Changed param name
    {
        StringBuilder sb = new StringBuilder();
        string formatString = "F" + TransformCapturePreferences.FloatPrecisionForOutputSetting;

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
            Debug.LogWarning($"Door '{doorObject.name}': Renderer not found or bounds are zero. Using placeholder dimensions (Width: {width.ToString(formatString, CultureInfo.InvariantCulture)}f, Height: {height.ToString(formatString, CultureInfo.InvariantCulture)}f).");
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
        string formattedPosition = $"new Vector3({positionToOutput.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString(formatString, CultureInfo.InvariantCulture)}f)";

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
        sb.AppendLine($"    width = {width.ToString(formatString, CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    height = {height.ToString(formatString, CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    position = {formattedPosition};{positionComment}");
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

    /// <summary>
    /// Formats the data of a given window GameObject into a C# string representation for a WindowSpec structure.
    /// Handles coordinate space conversion and calculates sill height relative to the room floor.
    /// </summary>
    /// <param name="windowObject">The GameObject representing the window.</param>
    /// <param name="roomFloorY">The Y coordinate of the room's floor, in world space, used for sill height calculation.</param>
    /// <returns>A C# string snippet representing the window specification.</returns>
    public string FormatAsWindowSpec(GameObject windowObject, float roomFloorY) // Added roomFloorY, changed param name
    {
        StringBuilder sb = new StringBuilder();
        string formatString = "F" + TransformCapturePreferences.FloatPrecisionForOutputSetting;

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
            Debug.LogWarning($"Window '{windowObject.name}': Renderer not found or bounds are zero. Using placeholder dimensions (Width: {width.ToString(formatString, CultureInfo.InvariantCulture)}f, Height: {height.ToString(formatString, CultureInfo.InvariantCulture)}f).");
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
        string formattedPosition = $"new Vector3({positionToOutput.x.ToString(formatString, CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString(formatString, CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString(formatString, CultureInfo.InvariantCulture)}f)";

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
        sb.AppendLine($"    width = {width.ToString(formatString, CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    height = {height.ToString(formatString, CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    position = {formattedPosition};{positionComment}");
        sb.AppendLine($"    sillHeight = {sillHeight.ToString(formatString, CultureInfo.InvariantCulture)}f,");
        sb.AppendLine($"    wallId = \"{wallId}\", {wallIdComment}"); // NEW LINE
        sb.AppendLine($"    isOperable = {isOperable.ToString().ToLowerInvariant()},");
        sb.AppendLine($"    bayPanes = {bayPanes.ToString(CultureInfo.InvariantCulture)},"); // int, no "f"
        sb.AppendLine($"    bayProjectionDepth = {bayProjectionDepth.ToString(formatString, CultureInfo.InvariantCulture)}f");
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
        generatedCode += $"Scene data capture attempt finished. Rooms: {capturedData.rooms.Count}, Walls: {capturedData.walls.Count}, Doors: {capturedData.doors.Count}, Windows: {capturedData.windows.Count}, Openings: {capturedData.openings.Count}\n";
        Repaint(); // Update UI

        generatedCode += "Performing comparison with HousePlanDiffer...\n";
        Repaint(); // Update UI

        DiffResultSet diffResult = HousePlanDiffer.ComparePlanToScene(
            loadedPlan,
            capturedData.rooms,
            capturedData.doors,
            capturedData.windows,
            capturedData.openings,
            capturedData.walls // Add this
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

    private (List<RoomData> rooms, List<DoorSpec> doors, List<WindowSpec> windows, List<OpeningSpec> openings, List<WallSegment> walls) CaptureSceneDataAsStructs(HousePlanSO existingPlanForContext)
    {
        InitializeCaches();
        generatedCode += "\nStarting scene data capture...\n";
        List<RoomData> capturedRooms = new List<RoomData>();
        List<DoorSpec> capturedDoors = new List<DoorSpec>();
        List<WindowSpec> capturedWindows = new List<WindowSpec>();
        List<OpeningSpec> capturedOpenings = new List<OpeningSpec>();
        List<WallSegment> capturedWalls = new List<WallSegment>();

        // Use existing FindAllHouseComponents to get all relevant GameObjects.
        // This method might need refinement if it doesn't find all desired objects or finds too many.
        List<GameObject> allGameObjects = new List<GameObject>();
            if (captureMode == CaptureMode.SelectedObjects)
        {
            allGameObjects.AddRange(Selection.gameObjects);
            Debug.Log($"Capturing {allGameObjects.Count} selected objects.");
        }
            else
        {
            // Fallback to the original full-scene scan logic
            allGameObjects = FindAllHouseComponents();
        }

        // Default values from existing plan context if available
        float storyHeight = existingPlanForContext?.storyHeight ?? 2.7f;
        float defaultWallThickness = existingPlanForContext?.exteriorWallThickness ?? 0.15f; // Default, might need interior too

        foreach (GameObject go in allGameObjects)
        {
            HouseComponentType componentType = DetectComponentType(go);

            switch (componentType)
            {
                case HouseComponentType.Room:
                {
                    RoomData roomData = new RoomData();
                    roomData.roomId = go.name; 
                    roomData.roomLabel = go.name; 

                    Renderer roomRenderer = go.GetComponent<Renderer>();
                    if (roomRenderer != null) {
                        roomData.dimensions = new Vector2(roomRenderer.bounds.size.x, roomRenderer.bounds.size.z);
                        roomData.position = new Vector3(roomRenderer.bounds.center.x, GetRoomFloorY(go), roomRenderer.bounds.center.z - roomRenderer.bounds.extents.z);
                    } else {
                        roomData.dimensions = new Vector2(1,1);
                        roomData.position = go.transform.position;
                        Debug.LogWarning($"Room '{go.name}' has no Renderer. Using default dimensions and transform position.");
                    }

                    roomData.notes = "";
                    roomData.connectedRoomIds = new List<string>();
                    roomData.atticHatchLocalPosition = Vector3.zero;
                    roomData.walls = new List<WallSegment>();

                    float roomFloorY = GetRoomFloorY(go);

                    foreach (Transform childTransform in go.transform)
                    {
                        if (DetectComponentType(childTransform.gameObject) == HouseComponentType.Wall)
                        {
                            GameObject wallGO = childTransform.gameObject;
                            WallSegmentAnalyzer.AnalyzedWallData analyzedWall = WallSegmentAnalyzer.AnalyzeWallGeometry(
                                wallGO,
                                roomFloorY,
                                storyHeight,
                                defaultWallThickness
                            );

                            WallSegment wallSeg = new WallSegment();
                            
                            Vector3 center = wallGO.transform.position;
                            Vector3 halfLengthDir = wallGO.transform.right * analyzedWall.wallLength / 2f;
                            wallSeg.startPoint = center - halfLengthDir;
                            wallSeg.endPoint = center + halfLengthDir;

                            wallSeg.thickness = analyzedWall.determinedThickness;
                            wallSeg.isExterior = analyzedWall.isLikelyExterior;
                            wallSeg.side = WallSide.North;

                            wallSeg.doorIdsOnWall = new List<string>();
                            wallSeg.windowIdsOnWall = new List<string>();
                            wallSeg.openingIdsOnWall = new List<string>();
                            
                            foreach (Transform itemOnWallTransform in wallGO.transform)
                            {
                                GameObject itemGO = itemOnWallTransform.gameObject;
                                HouseComponentType itemType = DetectComponentType(itemGO);
                                string itemId = itemGO.name; 

                                if (itemType == HouseComponentType.Door) wallSeg.doorIdsOnWall.Add(itemId);
                                else if (itemType == HouseComponentType.Window) wallSeg.windowIdsOnWall.Add(itemId);
                                else if (itemType == HouseComponentType.Unknown) wallSeg.openingIdsOnWall.Add(itemId);
                            }

                            if (analyzedWall.openings != null)
                            {
                                int openingIdx = 0;
                                foreach (var openingData in analyzedWall.openings)
                                {
                                    OpeningSpec os = new OpeningSpec();
                                    os.openingId = $"{wallGO.name}_AnalyzedOpening_{openingIdx++}";

                                    if (openingData.isDoorLike)
                                    {
                                        os.type = global::OpeningType.CasedOpening;
                                    }
                                    else if (openingData.isWindowLike)
                                    {
                                        os.type = global::OpeningType.CasedOpening;
                                    }
                                    else
                                    {
                                        os.type = global::OpeningType.CasedOpening;
                                        UnityEngine.Debug.LogWarning($"Opening {os.openingId} on wall {wallGO.name} was not classified as door-like or window-like by WallSegmentAnalyzer. Defaulting to CasedOpening.");
                                    }
                                    os.width = openingData.width;
                                    os.height = openingData.height;
                                    os.position = wallGO.transform.TransformPoint(openingData.localPosition);
                                    os.wallId = wallGO.name;

                                    bool alreadyCaptured = capturedOpenings.Any(co => co.openingId == os.openingId);
                                    if(!alreadyCaptured) capturedOpenings.Add(os); 

                                    if(!wallSeg.openingIdsOnWall.Contains(os.openingId)) wallSeg.openingIdsOnWall.Add(os.openingId);
                                }
                            }
                            wallSeg.wallId = wallGO.name;
                            roomData.walls.Add(wallSeg);
                        }
                    }
                    capturedRooms.Add(roomData);
                    break;
                }
                case HouseComponentType.Door:
                {
                    DoorSpec doorSpec = new DoorSpec();
                    doorSpec.doorId = go.name;
                    doorSpec.position = go.transform.position;

                    Renderer doorRenderer = go.GetComponent<Renderer>();
                    doorSpec.width = doorRenderer != null ? doorRenderer.bounds.size.x : 0.8f;
                    doorSpec.height = doorRenderer != null ? doorRenderer.bounds.size.y : 2.0f;

                    if (go.name.ToLower().Contains("pocket")) doorSpec.type = global::DoorType.Pocket;
                    else if (go.name.ToLower().Contains("bifold")) doorSpec.type = global::DoorType.BiFold;
                    else if (go.name.ToLower().Contains("overhead")) doorSpec.type = global::DoorType.Overhead;
                    else if (go.name.ToLower().Contains("sliding")) doorSpec.type = global::DoorType.Sliding;
                    else doorSpec.type = global::DoorType.Hinged;

                    var slidingController = go.GetComponent("SlidingDoorController");
                    if (slidingController != null) {
                        doorSpec.type = global::DoorType.Sliding;
                    }

                    if (doorSpec.type == global::DoorType.Sliding) {
                        doorSpec.slideDirection = global::SlideDirection.SlidesLeft;
                        doorSpec.swingDirection = global::SwingDirection.InwardNorth;
                    } else {
                        doorSpec.swingDirection = global::SwingDirection.InwardEast;
                        doorSpec.slideDirection = global::SlideDirection.SlidesLeft;
                    }

                    doorSpec.isExterior = go.name.ToLower().Contains("exterior");

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

                    capturedDoors.Add(doorSpec);
                    break;
                }
                case HouseComponentType.Window:
                {
                    WindowSpec windowSpec = new WindowSpec();
                    windowSpec.windowId = go.name;
                    windowSpec.position = go.transform.position;

                    Renderer windowRenderer = go.GetComponent<Renderer>();
                    windowSpec.width = windowRenderer != null ? windowRenderer.bounds.size.x : 1.2f;
                    windowSpec.height = windowRenderer != null ? windowRenderer.bounds.size.y : 1.0f;

                    float parentRoomFloorY = 0f;
                    bool floorYFound = false;

                    GameObject roomForFloorContext = null;
                    if (go.transform.parent != null) {
                        GameObject parentObj = go.transform.parent.gameObject;
                        if (DetectComponentType(parentObj) == HouseComponentType.Wall) {
                            if (parentObj.transform.parent != null) {
                                roomForFloorContext = parentObj.transform.parent.gameObject;
                            } else {
                                roomForFloorContext = GetRoomContext(go);
                                if(roomForFloorContext != null) Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent wall '{parentObj.name}' has no parent room. Using window's direct room context '{roomForFloorContext.name}'.");
                                else Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent wall '{parentObj.name}' has no parent room, and window has no direct room context.");
                            }
                        } else {
                            roomForFloorContext = GetRoomContext(go);
                             if(roomForFloorContext != null) Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent '{parentObj.name}' is not a wall. Using window's direct room context '{roomForFloorContext.name}'.");
                             else Debug.LogWarning($"TransformCaptureWindow: Window '{go.name}'s parent '{parentObj.name}' is not a wall, and window has no direct room context.");
                        }
                    } else {
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

                    if (go.name.ToLower().Contains("bay")) windowSpec.type = global::WindowType.Bay;
                    else if (go.name.ToLower().Contains("sliding")) windowSpec.type = global::WindowType.Sliding;
                    else if (go.name.ToLower().Contains("skylight")) windowSpec.type = global::WindowType.SkylightQuad;
                    else windowSpec.type = global::WindowType.SingleHung;

                    windowSpec.isOperable = true;
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
                case HouseComponentType.Wall:
                {
                    float storyHeightForWall = existingPlanForContext?.storyHeight ?? 2.7f;
                    float defaultWallThicknessForWall = existingPlanForContext?.exteriorWallThickness ?? 0.15f;

                    float wallRoomFloorY = 0f;
                    GameObject wallRoomContext = GetRoomContext(go);
                    if (wallRoomContext != null) {
                        wallRoomFloorY = GetRoomFloorY(wallRoomContext);
                    } else {
                        wallRoomFloorY = go.transform.position.y;
                        Debug.LogWarning($"Wall '{go.name}' has no room context. Using its own Y position ({wallRoomFloorY}) as floor Y for analysis. StoryHeight: {storyHeightForWall}, Thickness: {defaultWallThicknessForWall}");
                    }

                    WallSegmentAnalyzer.AnalyzedWallData analyzedWall = WallSegmentAnalyzer.AnalyzeWallGeometry(
                        go,
                        wallRoomFloorY,
                        storyHeightForWall,
                        defaultWallThicknessForWall
                    );

                    WallSegment wallSeg = new WallSegment();

                    Vector3 center = go.transform.position;
                    Vector3 halfLengthDir = go.transform.right * analyzedWall.wallLength / 2f;
                    wallSeg.startPoint = center - halfLengthDir;
                    wallSeg.endPoint = center + halfLengthDir;

                    wallSeg.thickness = analyzedWall.determinedThickness;
                    wallSeg.isExterior = analyzedWall.isLikelyExterior;

                    wallSeg.doorIdsOnWall = new List<string>();
                    wallSeg.windowIdsOnWall = new List<string>();
                    wallSeg.openingIdsOnWall = new List<string>();

                    foreach (Transform itemOnWallTransform in go.transform)
                    {
                        GameObject itemGO = itemOnWallTransform.gameObject;
                        HouseComponentType itemType = DetectComponentType(itemGO);
                        string itemId = itemGO.name;
                        if (itemType == HouseComponentType.Door) wallSeg.doorIdsOnWall.Add(itemId);
                        else if (itemType == HouseComponentType.Window) wallSeg.windowIdsOnWall.Add(itemId);
                    }

                    if (analyzedWall.openings != null)
                    {
                        int openingIdx = 0;
                        foreach (var openingData in analyzedWall.openings)
                        {
                            OpeningSpec os = new OpeningSpec();
                            os.openingId = $"{go.name}_AnalyzedOpening_{openingIdx++}";

                            if (openingData.isDoorLike) os.type = global::OpeningType.CasedOpening;
                            else if (openingData.isWindowLike) os.type = global::OpeningType.CasedOpening;
                            else os.type = global::OpeningType.CasedOpening;

                            os.width = openingData.width;
                            os.height = openingData.height;
                            os.position = go.transform.TransformPoint(openingData.localPosition);
                            os.wallId = go.name;

                            bool alreadyCapturedOpen = capturedOpenings.Any(co => co.openingId == os.openingId);
                            if(!alreadyCapturedOpen) capturedOpenings.Add(os);

                            if(!wallSeg.openingIdsOnWall.Contains(os.openingId)) wallSeg.openingIdsOnWall.Add(os.openingId);
                        }
                    }
                    wallSeg.wallId = go.name;
                    capturedWalls.Add(wallSeg);
                    break;
                }
            }
        }
        generatedCode += $"Finished scene data capture. Rooms: {capturedRooms.Count}, Doors: {capturedDoors.Count}, Windows: {capturedWindows.Count}, Openings: {capturedOpenings.Count}, Walls: {capturedWalls.Count}.\n";
        Repaint();
        return (capturedRooms, capturedDoors, capturedWindows, capturedOpenings, capturedWalls);
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

    private static string CreateOrUpdateBackup(UnityEngine.Object planToBackup, string originalAssetPath)
    {
        if (planToBackup == null || string.IsNullOrEmpty(originalAssetPath))
        {
            Debug.LogError("CreateOrUpdateBackup: Plan to backup or original asset path is null or empty.");
            return string.Empty;
        }

        try
        {
            string assetName = Path.GetFileNameWithoutExtension(originalAssetPath);
            string backupDirectory = "Assets/BlueprintData/Backups";

            // Create the backup directory if it doesn't exist
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"{assetName}_{timestamp}.asset";
            string backupAssetPath = Path.Combine(backupDirectory, backupFileName);

            // Ensure the asset to backup exists at the original path before copying
            if (!File.Exists(originalAssetPath)) // Or AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(originalAssetPath) != null
            {
                Debug.LogError($"CreateOrUpdateBackup: Original asset not found at path: {originalAssetPath}");
                return string.Empty;
            }

            // Copy the asset to the backup path
            if (AssetDatabase.CopyAsset(originalAssetPath, backupAssetPath))
            {
                Debug.Log($"Backup created successfully: {backupAssetPath}");
                return backupAssetPath;
            }
            else
            {
                Debug.LogError($"CreateOrUpdateBackup: Failed to copy asset from '{originalAssetPath}' to '{backupAssetPath}'.");
                return string.Empty;
            }
        }
        catch (IOException ex)
        {
            Debug.LogError($"CreateOrUpdateBackup: An IO Exception occurred. Path: {originalAssetPath}. Error: {ex.Message}");
            return string.Empty;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"CreateOrUpdateBackup: An unexpected error occurred. Path: {originalAssetPath}. Error: {ex.Message}");
            return string.Empty;
        }
    }

    private void ExecuteUpdateOnAsset()
    {
        Debug.Log("Initiating asset update process...");
        string targetAssetPath = "Assets/BlueprintData/NewHousePlan.asset"; // Hardcoded for now
        string assetNameForDialog = Path.GetFileName(targetAssetPath); // Use actual asset name for dialogs

        // --- Placeholder for Diffing ---
        Debug.Log("Performing comparison with current plan...");
        // This part of the code in ExecuteUpdateOnAsset seems to be placeholder/dummy data.
        // The actual diffing happens in CompareWithPlan() which populates generatedCode.
        // For ExecuteUpdateOnAsset, we need to get the *actual* DiffResultSet that was generated by CompareWithPlan.
        // This requires a more significant refactor to store and retrieve the result of CompareWithPlan.
        // For now, to fix the compile error, I will adapt the placeholder to use the correct DiffResultSet structure,
        // but acknowledge that the logic of *obtaining* this diffResult in ExecuteUpdateOnAsset is flawed.

        // Attempt to get a meaningful diffResult. For now, this will be a new empty one.
        // A proper solution would involve storing the result from CompareWithPlan().
        HousePlanSO loadedPlanForUpdate = HousePlanDiffer.LoadTargetHousePlan(targetAssetPath);
        DiffResultSet diffResultForUpdate;
        if (loadedPlanForUpdate != null) {
            var capturedDataForUpdate = CaptureSceneDataAsStructs(loadedPlanForUpdate);
            diffResultForUpdate = HousePlanDiffer.ComparePlanToScene(
                loadedPlanForUpdate,
                capturedDataForUpdate.rooms,
                capturedDataForUpdate.doors,
                capturedDataForUpdate.windows,
                capturedDataForUpdate.openings,
                capturedDataForUpdate.walls // Add this
            );
        } else {
            diffResultForUpdate = new DiffResultSet(); // Fallback to empty if plan load fails
            Debug.LogWarning("ExecuteUpdateOnAsset: Could not load plan to generate a diff for update. Using empty diff.");
        }

        // Correctly count changes from the DiffResultSet structure
        int additions = 0;
        int modifications = 0;
        int removals = 0;

        if (diffResultForUpdate != null) {
            System.Action<System.Collections.IEnumerable> countChanges = (collection) => {
                if (collection == null) return;
                foreach (var item in collection) {
                    var changeProperty = item.GetType().GetProperty("change");
                    if (changeProperty != null) {
                        ChangeType changeType = (ChangeType)changeProperty.GetValue(item);
                        switch (changeType) {
                            case ChangeType.Added: additions++; break;
                            case ChangeType.Modified: modifications++; break;
                            case ChangeType.Removed: removals++; break;
                        }
                    }
                }
            };

            countChanges(diffResultForUpdate.roomDiffs);
            countChanges(diffResultForUpdate.wallDiffs);
            countChanges(diffResultForUpdate.doorDiffs);
            countChanges(diffResultForUpdate.windowDiffs);
            countChanges(diffResultForUpdate.openingDiffs);
        }
        // The rest of the ExecuteUpdateOnAsset method continues from here using these counts...
        // ... (original code for DisplayDialog, backup, etc.)
        // The part that applies changes (loadedPlan.rooms.Add(item);) also needs to be updated
        // to work with the actual diff entries, not dummy string lists.
        // This is a larger logic change. For now, the placeholder logic for applying changes will likely fail at runtime
        // or do incorrect things. The immediate goal is to fix compile errors.

        // Placeholder for applying actual changes from diffResultForUpdate.
        // The original code was:
        // foreach (var item in diffResult.AddedItems) { loadedPlan.rooms.Add(item); }
        // This needs to be replaced with logic that iterates through diffResultForUpdate.roomDiffs etc.
        // and applies changes based on entry.change and entry.capturedData or entry.existingData.
        // For fixing the compile error with minimal changes to existing placeholder logic that used AddedItems:
        // We'll clear the existing placeholder logic that tries to add strings to loadedPlan.rooms.
        // This part of the code is clearly a placeholder and needs a full rewrite.
        // The original `diffResult.AddedItems.Count` etc. was based on a dummy `DiffResultSet` constructor.
        // The following lines that used `diffResult.AddedItems` (e.g. `foreach (var item in diffResult.AddedItems)`)
        // also need to be addressed. To fix the immediate compile error, I will remove the dummy operations.
        // The subtask will focus on the counting and the constructor. The application part is a deeper issue.
        // So, the section from "foreach (var item in diffResult.AddedItems)" up to
        // "Debug.Log($"Simulated changes applied to '{assetNameForDialog}'. Rooms count: {loadedPlan.rooms.Count}");"
        // will be commented out or replaced with a simple log message indicating changes need to be applied from diffResultForUpdate.
        // For the subtask, let's replace it with a log.
        // The lines to replace are roughly 1961-1979 in the original file.
        // New placeholder:
        // Debug.Log("Applying changes from diffResultForUpdate to loadedPlan - This part needs full implementation.");
        // if (diffResultForUpdate != null && loadedPlan != null)
        // {
        //     // Example:
        //     // foreach(var roomEntry in diffResultForUpdate.roomDiffs.Where(e => e.change == ChangeType.Added)) { loadedPlan.rooms.Add(roomEntry.capturedData); }
        //     // (Actual implementation would be more complex, handling all types and changes)
        // }
        // This change will be part of the subtask.

        Debug.Log($"Diffing complete. Found {additions} additions, {modifications} modifications, {removals} removals for '{assetNameForDialog}'.");

        if (additions == 0 && modifications == 0 && removals == 0)
        {
            EditorUtility.DisplayDialog("No Changes", $"No changes detected between the scene and '{assetNameForDialog}'.", "OK");
            return;
        }

        // --- Confirmation Dialog ---
        bool userConfirmed = EditorUtility.DisplayDialog(
            "Confirm Asset Update",
            $"Apply {additions} additions, {modifications} modifications, {removals} removals to '{assetNameForDialog}'?",
            "Apply",
            "Cancel"
        );

        if (!userConfirmed)
        {
            Debug.Log("Asset update cancelled by user.");
            EditorUtility.DisplayDialog("Cancelled", "Asset update cancelled by user.", "OK");
            return;
        }

        // Main operation wrapped in try-finally to ensure progress bar is cleared
        try
        {
            // --- Backup ---
            string backupPath = string.Empty;
            bool backupAttempted = false; // Renamed from backupSucceeded to reflect attempt
            UnityEngine.Object planToBackupForBackupMethod = null; // Defined here for wider scope if needed

            try
            {
                planToBackupForBackupMethod = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetAssetPath);
                bool assetExists = planToBackupForBackupMethod != null;

                if (assetExists)
                {
                    Debug.Log($"Backup in progress for '{assetNameForDialog}'...");
                    backupAttempted = true;
                    backupPath = CreateOrUpdateBackup(planToBackupForBackupMethod, targetAssetPath);
                    if (string.IsNullOrEmpty(backupPath))
                    {
                        EditorUtility.DisplayDialog("Backup Failed", $"Failed to create backup for '{assetNameForDialog}'. Check console for errors. Aborting update.", "OK");
                        Debug.Log("Asset update aborted due to backup error.");
                        return;
                    }
                    Debug.Log("Asset backup successful: " + backupPath);
                }
                else
                {
                    Debug.Log($"Asset '{assetNameForDialog}' does not exist. No backup will be created (assuming new asset creation).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ExecuteUpdateOnAsset: Exception during backup phase for '{assetNameForDialog}'. Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                EditorUtility.DisplayDialog("Backup Error", $"An unexpected error occurred during the backup process for '{assetNameForDialog}'. Check console for details. Aborting update.", "OK");
                Debug.Log("Asset update aborted due to backup error.");
                return;
            }

            // --- Load Asset (Placeholder) ---
            HousePlanSO loadedPlan = null;
            bool isNewAsset = false;
            try
            {
                Debug.Log($"Loading asset '{assetNameForDialog}'...");
                loadedPlan = AssetDatabase.LoadAssetAtPath<HousePlanSO>(targetAssetPath);

                if (loadedPlan == null)
                {
                    Debug.Log($"Asset '{assetNameForDialog}' not found. Assuming creation of a new asset.");
                    loadedPlan = ScriptableObject.CreateInstance<HousePlanSO>();
                    isNewAsset = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ExecuteUpdateOnAsset: Exception during asset loading phase for '{assetNameForDialog}'. Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                EditorUtility.DisplayDialog("Asset Load Error", $"An unexpected error occurred while loading '{assetNameForDialog}'. Check console for details. Aborting update.", "OK");
                Debug.Log("Asset update aborted due to loading error.");
                return;
            }

            if (loadedPlan == null)
            {
                Debug.LogError($"ExecuteUpdateOnAsset: Failed to load or create asset '{assetNameForDialog}' (loadedPlan is null after attempts).");
                EditorUtility.DisplayDialog("Load Failed", $"Critical error: Failed to load or create asset '{assetNameForDialog}'. Aborting update.", "OK");
                Debug.Log("Asset update aborted due to critical loading error.");
                return;
            }

            // --- Record Undo ---
            try
            {
                if (!isNewAsset)
                {
                    // UnityEditor.Undo.RecordObject(loadedPlan, "Update House Plan from Capture Tool");
                    Debug.Log($"Undo recorded for '{assetNameForDialog}' (placeholder).");
                }
                else
                {
                    Debug.Log($"Skipping Undo for new asset '{assetNameForDialog}'.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ExecuteUpdateOnAsset: Exception during Undo recording for '{assetNameForDialog}'. Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }

            // --- Apply Changes (Placeholder Logic) ---
            try
            {
                Debug.Log($"Applying {additions} additions, {modifications} modifications, {removals} removals to '{assetNameForDialog}'...");
                EditorUtility.DisplayProgressBar("Applying Changes", "Processing items...", 0f);

                Debug.Log("Applying changes from diffResultForUpdate to loadedPlan - This part needs full implementation.");
                if (diffResultForUpdate != null && loadedPlan != null)
                {
                    // Example:
                    // foreach(var roomEntry in diffResultForUpdate.roomDiffs.Where(e => e.change == ChangeType.Added)) { loadedPlan.rooms.Add(roomEntry.capturedData); }
                    // (Actual implementation would be more complex, handling all types and changes)
                    Debug.Log($"Placeholder: Would apply changes. Rooms count before potential changes: {loadedPlan.rooms.Count}");
                }
                // Original placeholder logic for applying changes has been removed as per subtask.

            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ExecuteUpdateOnAsset: Exception during 'Apply Changes' phase for '{assetNameForDialog}'. Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                string backupMessage = backupAttempted && !string.IsNullOrEmpty(backupPath) ? $"Consider reverting to backup: {backupPath}" : "No backup was created or it failed.";
                EditorUtility.DisplayDialog("Error Applying Changes", $"An error occurred while applying changes to '{assetNameForDialog}'. Check console for details. {backupMessage}", "OK");
                Debug.Log("Asset update aborted due to error during change application.");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // --- Save Asset (Placeholder) ---
            try
            {
                Debug.Log($"Saving changes to '{assetNameForDialog}'...");
                EditorUtility.DisplayProgressBar("Saving Asset", "Persisting changes...", 0.9f);
                if (isNewAsset)
                {
                    // UnityEditor.AssetDatabase.CreateAsset(loadedPlan, targetAssetPath);
                    Debug.Log($"New asset '{assetNameForDialog}' created at '{targetAssetPath}' (placeholder).");
                }
                else
                {
                    // UnityEditor.EditorUtility.SetDirty(loadedPlan);
                    Debug.Log($"Asset '{assetNameForDialog}' marked dirty (placeholder).");
                }
                // UnityEditor.AssetDatabase.SaveAssets();
                // UnityEditor.AssetDatabase.Refresh();
                Debug.Log($"Asset '{assetNameForDialog}' saved and refreshed (placeholder).");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ExecuteUpdateOnAsset: Exception during 'Save Asset' phase for '{assetNameForDialog}'. Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                string backupMessage = backupAttempted && !string.IsNullOrEmpty(backupPath) ? $"Consider reverting to backup: {backupPath}" : "No backup was created or it failed.";
                EditorUtility.DisplayDialog("Error Saving Asset", $"An error occurred while saving '{assetNameForDialog}'. Changes might not be fully saved. Check console for details. {backupMessage}", "OK");
                Debug.Log("Asset update aborted due to error during save.");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // --- Completion Message ---
            EditorUtility.DisplayDialog("Success", $"Asset '{assetNameForDialog}' {(isNewAsset ? "created" : "updated")} successfully (placeholder operations).", "OK");
            Debug.Log($"Asset update complete for '{assetNameForDialog}'.");

        } // End of main try block
        finally
        {
            EditorUtility.ClearProgressBar(); // Ensure progress bar is cleared in all cases
            Debug.Log("ExecuteUpdateOnAsset finished."); // General finish log
        }
    }

    void OnFocus()
    {
        UpdateRecentlySelected();
        // Repaint scene if gizmos are active to reflect selection changes
        if (showCaptureGizmos) SceneView.RepaintAll();
    }

    void OnLostFocus()
    {
        recentlySelectedForCapture.Clear();
        // Repaint scene if gizmos are active to clear selection highlights
        if (showCaptureGizmos) SceneView.RepaintAll();
    }

    private void UpdateRecentlySelected()
    {
        recentlySelectedForCapture.Clear();
        if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                recentlySelectedForCapture.Add(go);
            }
        }
    }

    void OnSelectionChange()
    {
        if (EditorWindow.focusedWindow == this)
        {
            UpdateRecentlySelected();
            if (showCaptureGizmos) SceneView.RepaintAll();
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawCaptureGizmos(Transform transform, GizmoType gizmoType)
    {
        if (!showCaptureGizmos)
        {
            return;
        }

        GameObject gameObject = transform.gameObject;
        string statusLabel = string.Empty;
        Color gizmoColor = Color.clear; // Default to clear if not in any list

        bool isFocused = EditorWindow.focusedWindow is TransformCaptureWindow;

        if (objectsWithCaptureErrorsLastRun.Contains(gameObject))
        {
            gizmoColor = Color.red;
            statusLabel = "Status: Error";
        }
        else if (successfullyCapturedLastRun.Contains(gameObject))
        {
            gizmoColor = Color.green;
            statusLabel = "Status: Captured";
        }
        else if (isFocused && recentlySelectedForCapture.Contains(gameObject))
        {
            gizmoColor = Color.yellow;
            statusLabel = "Status: Pending Selection";
        }

        if (gizmoColor != Color.clear)
        {
            Handles.color = gizmoColor;
            Vector3 size = Vector3.one * 0.5f; // Default size
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                size = renderer.bounds.size;
                // Ensure size is not zero, which can happen for some renderers or uninitialized objects
                if (size.x == 0) size.x = 0.5f;
                if (size.y == 0) size.y = 0.5f;
                if (size.z == 0) size.z = 0.5f;
            }

            // Draw the wire cube at the object's pivot point (transform.position)
            Handles.DrawWireCube(transform.position, size);

            HouseComponentType compType = DetectComponentType(gameObject);
            string finalLabel = $"Type: {compType}\n{statusLabel}";

            Handles.Label(transform.position + Vector3.up * (size.y * 0.5f + 0.2f), finalLabel);
        }
    }
}

class TransformCaptureToolSettingsProvider
{
    [SettingsProvider]
    public static SettingsProvider CreateTransformCaptureToolSettingsProvider()
    {
        var provider = new SettingsProvider("Preferences/Transform Capture Tool", SettingsScope.User)
        {
            label = "Transform Capture Tool",
            guiHandler = (searchContext) =>
            {
                TransformCapturePreferences.LoadPreferences(); // Ensure we have the latest from EditorPrefs

                EditorGUI.BeginChangeCheck();

                TransformCaptureWindow.CoordinateSpaceSetting newCoordSpace =
                    (TransformCaptureWindow.CoordinateSpaceSetting)EditorGUILayout.EnumPopup(
                        "Default Coordinate Space", TransformCapturePreferences.DefaultCoordinateSpaceSetting);

                bool newUseContextual = EditorGUILayout.Toggle(
                    "Default Use Contextual Formatting", TransformCapturePreferences.DefaultUseContextualFormattingSetting);

                bool newGroupByRoom = EditorGUILayout.Toggle(
                    "Default Group by Room", TransformCapturePreferences.DefaultGroupByRoomSetting);

                int newFloatPrecision = EditorGUILayout.IntSlider(
                    "Float Precision (decimals)", TransformCapturePreferences.FloatPrecisionForOutputSetting, 1, 7); // Min 1, Max 7 decimal places

                if (EditorGUI.EndChangeCheck())
                {
                    TransformCapturePreferences.DefaultCoordinateSpaceSetting = newCoordSpace;
                    TransformCapturePreferences.DefaultUseContextualFormattingSetting = newUseContextual;
                    TransformCapturePreferences.DefaultGroupByRoomSetting = newGroupByRoom;
                    TransformCapturePreferences.FloatPrecisionForOutputSetting = newFloatPrecision;

                    TransformCapturePreferences.SavePreferences();
                }
            },

            keywords = new HashSet<string>(new[] { "Transform", "Capture", "House", "Tool", "Coordinate", "Formatting", "Precision" })
        };

        return provider;
    }
}

public static class TransformCapturePreferences
{
    private const string k_DefaultCoordinateSpace = "TransformCaptureTool.DefaultCoordinateSpace";
    private const string k_DefaultUseContextualFormatting = "TransformCaptureTool.DefaultUseContextualFormatting";
    private const string k_DefaultGroupByRoom = "TransformCaptureTool.DefaultGroupByRoom";
    private const string k_FloatPrecision = "TransformCaptureTool.FloatPrecision";

    // Default values for preferences
    public static TransformCaptureWindow.CoordinateSpaceSetting DefaultCoordinateSpaceSetting { get; set; } = TransformCaptureWindow.CoordinateSpaceSetting.World;
    public static bool DefaultUseContextualFormattingSetting { get; set; } = false;
    public static bool DefaultGroupByRoomSetting { get; set; } = false;
    public static int FloatPrecisionForOutputSetting { get; set; } = 3; // Default to 3 decimal places

    // Static constructor to load preferences once when class is accessed
    static TransformCapturePreferences()
    {
        LoadPreferences();
    }

    public static void LoadPreferences()
    {
        DefaultCoordinateSpaceSetting = (TransformCaptureWindow.CoordinateSpaceSetting)EditorPrefs.GetInt(k_DefaultCoordinateSpace, (int)TransformCaptureWindow.CoordinateSpaceSetting.World);
        DefaultUseContextualFormattingSetting = EditorPrefs.GetBool(k_DefaultUseContextualFormatting, false);
        DefaultGroupByRoomSetting = EditorPrefs.GetBool(k_DefaultGroupByRoom, false);
        FloatPrecisionForOutputSetting = EditorPrefs.GetInt(k_FloatPrecision, 3);
        // Ensure precision is within a sane range, e.g., 1-7, as slider will enforce but direct EditorPrefs edit might not.
        FloatPrecisionForOutputSetting = Mathf.Clamp(FloatPrecisionForOutputSetting, 1, 7);
    }

    public static void SavePreferences()
    {
        EditorPrefs.SetInt(k_DefaultCoordinateSpace, (int)DefaultCoordinateSpaceSetting);
        EditorPrefs.SetBool(k_DefaultUseContextualFormatting, DefaultUseContextualFormattingSetting);
        EditorPrefs.SetBool(k_DefaultGroupByRoom, DefaultGroupByRoomSetting);
        EditorPrefs.SetInt(k_FloatPrecision, FloatPrecisionForOutputSetting);
    }
}
