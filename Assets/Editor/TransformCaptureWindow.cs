using UnityEditor;
using UnityEngine;
using System.Text;
using System.Globalization; // Add this line

public class TransformCaptureWindow : EditorWindow
{
    public enum HouseComponentType { Unknown, Room, Wall, Door, Window, Foundation, Roof, ProceduralHouseRoot }

    private string generatedCode = "";
    private Vector2 scrollPosition;
    private bool useContextualFormatting = false; // Added field

    [MenuItem("House Tools/Transform Data Capturer")]
    public static void ShowWindow()
    {
        GetWindow<TransformCaptureWindow>("Transform Capturer");
    }

    void OnGUI()
    {
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
                        Vector3 position = obj.transform.position;
                        sb.AppendLine($"Vector3 position = new Vector3({position.x.ToString("f3", CultureInfo.InvariantCulture)}f, {position.y.ToString("f3", CultureInfo.InvariantCulture)}f, {position.z.ToString("f3", CultureInfo.InvariantCulture)}f);");
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
                Vector3 position = obj.transform.position;
                sb.AppendLine($"Vector3 position = new Vector3({position.x.ToString("f3", CultureInfo.InvariantCulture)}f, {position.y.ToString("f3", CultureInfo.InvariantCulture)}f, {position.z.ToString("f3", CultureInfo.InvariantCulture)}f);");
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
        // TODO: Implement full DoorSpec formatting
        return $"// Detected Door: {obj.name} - DoorSpec formatting pending.\n";
    }

    private string FormatAsWindowSpec(GameObject obj)
    {
        // TODO: Implement full WindowSpec formatting
        return $"// Detected Window: {obj.name} - WindowSpec formatting pending.\n";
    }
}
