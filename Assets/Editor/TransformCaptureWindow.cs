using UnityEditor;
using UnityEngine;
using System.Text;
using System.Globalization; // Add this line

public class TransformCaptureWindow : EditorWindow
{
    public enum HouseComponentType { Unknown, Room, Wall, Door, Window, Foundation, Roof, ProceduralHouseRoot }
    public enum CoordinateSpaceSetting { World, RoomRelative, WallRelative }

    private string generatedCode = "";
    private Vector2 scrollPosition;
    private CoordinateSpaceSetting selectedCoordinateSpace = CoordinateSpaceSetting.World;
    private bool useContextualFormatting = false; // Added field

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
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            sb.AppendLine("// No objects selected.");
            generatedCode = sb.ToString();
            return;
        }

        foreach (GameObject obj in selectedObjects)
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
                        sb.AppendLine(FormatAsWallSegment(obj));
                        break;
                    case HouseComponentType.Door:
                        sb.AppendLine(FormatAsDoorSpec(obj));
                        break;
                    case HouseComponentType.Window:
                        sb.AppendLine(FormatAsWindowSpec(obj));
                        break;
                    case HouseComponentType.Foundation:
                    case HouseComponentType.Roof:
                    case HouseComponentType.ProceduralHouseRoot:
                    case HouseComponentType.Unknown:
                    default:
                        sb.AppendLine($"// Detected {componentType} \"{obj.name}\" - Using generic transform output.");
                        // Generic transform output (existing code)
                        sb.AppendLine($"// Transform data for \"{obj.name}\" (InstanceID: {obj.GetInstanceID()})");
                        // --- Start of new position logic for generic path ---
                        Vector3 worldPosition = obj.transform.position;
                        Vector3 positionToOutput = worldPosition;
                        string positionComment = " // Position (World Space)";

                        switch (selectedCoordinateSpace)
                        {
                            case CoordinateSpaceSetting.RoomRelative:
                                GameObject roomObject = GetRoomContext(obj);
                                if (roomObject != null)
                                {
                                    Vector3 roomOrigin = GetRoomOrigin(obj); // GetRoomOrigin itself handles warning if roomObject is null via GetRoomContext
                                    positionToOutput = ConvertToRoomRelative(worldPosition, roomOrigin);
                                    positionComment = $" // Position (Room Relative to '{roomObject.name}')";
                                }
                                else if (obj.transform.parent != null)
                                {
                                    // Fallback to parent if no room context
                                    Vector3 parentOrigin = obj.transform.parent.position;
                                    positionToOutput = ConvertToRoomRelative(worldPosition, parentOrigin);
                                    positionComment = $" // Position (Relative to parent '{obj.transform.parent.name}')";
                                    Debug.LogWarning($"'{obj.name}' (Generic): RoomRelative selected, no room context. Outputting relative to parent '{obj.transform.parent.name}'.");
                                }
                                else
                                {
                                    // No room, no parent
                                    positionComment = " // Position (World Space - RoomRelative selected, but no Room Context or parent found)";
                                    Debug.LogWarning($"'{obj.name}' (Generic): RoomRelative selected, but no Room Context or parent found. Defaulting to World Space.");
                                }
                                break;

                            case CoordinateSpaceSetting.WallRelative:
                                if (obj.transform.parent != null)
                                {
                                    // Use parent as the wall/reference transform
                                    positionToOutput = ConvertToWallRelative(worldPosition, obj.transform.parent);
                                    positionComment = $" // Position (Relative to parent '{obj.transform.parent.name}' as wall context)";
                                    // No specific warning here as this is the defined fallback for generic WallRelative
                                }
                                else
                                {
                                    // No parent to use as reference
                                    positionComment = " // Position (World Space - WallRelative selected, but no parent found for relative conversion)";
                                    Debug.LogWarning($"'{obj.name}' (Generic): WallRelative selected, but no parent found. Defaulting to World Space.");
                                }
                                break;

                            case CoordinateSpaceSetting.World:
                            default:
                                // Position is already worldPosition, comment is already set
                                break;
                        }
                        sb.AppendLine($"Vector3 position = new Vector3({positionToOutput.x.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString("f3", CultureInfo.InvariantCulture)}f);{positionComment}");
                        // --- End of new position logic for generic path ---
                        Vector3 eulerAngles = obj.transform.eulerAngles;
                        sb.AppendLine($"Quaternion rotation = Quaternion.Euler({eulerAngles.x.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.y.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.z.ToString("f1", CultureInfo.InvariantCulture)}f); // World rotation");
                        Vector3 scale = obj.transform.localScale;
                        sb.AppendLine($"Vector3 scale = new Vector3({scale.x.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.y.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.z.ToString("f3", CultureInfo.InvariantCulture)}f); // Local scale");
                        sb.AppendLine();
                        break;
                }
            }
            else
            {
                // Original generic transform output
                sb.AppendLine($"// Transform data for \"{obj.name}\" (InstanceID: {obj.GetInstanceID()})");
                // --- Start of new position logic for generic path ---
                Vector3 worldPosition = obj.transform.position;
                Vector3 positionToOutput = worldPosition;
                string positionComment = " // Position (World Space)";

                switch (selectedCoordinateSpace)
                {
                    case CoordinateSpaceSetting.RoomRelative:
                        GameObject roomObject = GetRoomContext(obj);
                        if (roomObject != null)
                        {
                            Vector3 roomOrigin = GetRoomOrigin(obj); // GetRoomOrigin itself handles warning if roomObject is null via GetRoomContext
                            positionToOutput = ConvertToRoomRelative(worldPosition, roomOrigin);
                            positionComment = $" // Position (Room Relative to '{roomObject.name}')";
                        }
                        else if (obj.transform.parent != null)
                        {
                            // Fallback to parent if no room context
                            Vector3 parentOrigin = obj.transform.parent.position;
                            positionToOutput = ConvertToRoomRelative(worldPosition, parentOrigin);
                            positionComment = $" // Position (Relative to parent '{obj.transform.parent.name}')";
                            Debug.LogWarning($"'{obj.name}' (Generic): RoomRelative selected, no room context. Outputting relative to parent '{obj.transform.parent.name}'.");
                        }
                        else
                        {
                            // No room, no parent
                            positionComment = " // Position (World Space - RoomRelative selected, but no Room Context or parent found)";
                            Debug.LogWarning($"'{obj.name}' (Generic): RoomRelative selected, but no Room Context or parent found. Defaulting to World Space.");
                        }
                        break;

                    case CoordinateSpaceSetting.WallRelative:
                        if (obj.transform.parent != null)
                        {
                            // Use parent as the wall/reference transform
                            positionToOutput = ConvertToWallRelative(worldPosition, obj.transform.parent);
                            positionComment = $" // Position (Relative to parent '{obj.transform.parent.name}' as wall context)";
                            // No specific warning here as this is the defined fallback for generic WallRelative
                        }
                        else
                        {
                            // No parent to use as reference
                            positionComment = " // Position (World Space - WallRelative selected, but no parent found for relative conversion)";
                            Debug.LogWarning($"'{obj.name}' (Generic): WallRelative selected, but no parent found. Defaulting to World Space.");
                        }
                        break;

                    case CoordinateSpaceSetting.World:
                    default:
                        // Position is already worldPosition, comment is already set
                        break;
                }
                sb.AppendLine($"Vector3 position = new Vector3({positionToOutput.x.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.y.ToString("f3", CultureInfo.InvariantCulture)}f, {positionToOutput.z.ToString("f3", CultureInfo.InvariantCulture)}f);{positionComment}");
                // --- End of new position logic for generic path ---
                Vector3 eulerAngles = obj.transform.eulerAngles;
                sb.AppendLine($"Quaternion rotation = Quaternion.Euler({eulerAngles.x.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.y.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.z.ToString("f1", CultureInfo.InvariantCulture)}f); // World rotation");
                Vector3 scale = obj.transform.localScale;
                sb.AppendLine($"Vector3 scale = new Vector3({scale.x.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.y.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.z.ToString("f3", CultureInfo.InvariantCulture)}f); // Local scale");
                sb.AppendLine();
            }
        }
        generatedCode = sb.ToString();
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

    private string FormatAsRoomData(GameObject obj)
    {
        // TODO: Implement full RoomData formatting
        return $"// Detected Room: {obj.name} - RoomData formatting pending.\n";
    }

    private string FormatAsWallSegment(GameObject obj)
    {
        // TODO: Implement full WallSegment formatting
        return $"// Detected Wall: {obj.name} - WallSegment formatting pending.\n";
    }

    private string FormatAsDoorSpec(GameObject obj)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"// Door Specification for \"{obj.name}\" (InstanceID: {obj.GetInstanceID()})");

        Vector3 worldPosition = obj.transform.position;
        sb.AppendLine(GetProcessedPositionString(obj, worldPosition, "Door"));

        // Retain existing rotation and scale logic (world rotation, local scale)
        Vector3 eulerAngles = obj.transform.eulerAngles;
        sb.AppendLine($"Quaternion rotation = Quaternion.Euler({eulerAngles.x.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.y.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.z.ToString("f1", CultureInfo.InvariantCulture)}f); // World rotation");
        Vector3 scale = obj.transform.localScale;
        sb.AppendLine($"Vector3 scale = new Vector3({scale.x.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.y.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.z.ToString("f3", CultureInfo.InvariantCulture)}f); // Local scale");

        // TODO: Add other Door-specific properties here if needed in future
        sb.AppendLine("// Add other Door-specific properties here");
        sb.AppendLine();
        return sb.ToString();
    }

    private string FormatAsWindowSpec(GameObject obj)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"// Window Specification for \"{obj.name}\" (InstanceID: {obj.GetInstanceID()})");

        Vector3 worldPosition = obj.transform.position;
        sb.AppendLine(GetProcessedPositionString(obj, worldPosition, "Window"));

        // Retain existing rotation and scale logic (world rotation, local scale)
        Vector3 eulerAngles = obj.transform.eulerAngles;
        sb.AppendLine($"Quaternion rotation = Quaternion.Euler({eulerAngles.x.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.y.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.z.ToString("f1", CultureInfo.InvariantCulture)}f); // World rotation");
        Vector3 scale = obj.transform.localScale;
        sb.AppendLine($"Vector3 scale = new Vector3({scale.x.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.y.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.z.ToString("f3", CultureInfo.InvariantCulture)}f); // Local scale");

        // TODO: Add other Window-specific properties here
        sb.AppendLine("// Add other Window-specific properties here");
        sb.AppendLine();
        return sb.ToString();
    }
}
