using UnityEditor;
using UnityEngine;
using System.Text;
using System.Globalization; // Add this line

public class TransformCaptureWindow : EditorWindow
{
    private string generatedCode = "";
    private Vector2 scrollPosition;

    [MenuItem("House Tools/Transform Data Capturer")]
    public static void ShowWindow()
    {
        GetWindow<TransformCaptureWindow>("Transform Capturer");
    }

    void OnGUI()
    {
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
            sb.AppendLine($"// Transform data for \"{obj.name}\" (InstanceID: {obj.GetInstanceID()})");

            // Position
            Vector3 position = obj.transform.position;
            sb.AppendLine($"Vector3 position = new Vector3({position.x.ToString("f3", CultureInfo.InvariantCulture)}f, {position.y.ToString("f3", CultureInfo.InvariantCulture)}f, {position.z.ToString("f3", CultureInfo.InvariantCulture)}f);");

            // Rotation (World Euler Angles)
            Vector3 eulerAngles = obj.transform.eulerAngles;
            sb.AppendLine($"Quaternion rotation = Quaternion.Euler({eulerAngles.x.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.y.ToString("f1", CultureInfo.InvariantCulture)}f, {eulerAngles.z.ToString("f1", CultureInfo.InvariantCulture)}f); // World rotation");

            // Scale (Local)
            Vector3 scale = obj.transform.localScale;
            sb.AppendLine($"Vector3 scale = new Vector3({scale.x.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.y.ToString("f3", CultureInfo.InvariantCulture)}f, {scale.z.ToString("f3", CultureInfo.InvariantCulture)}f); // Local scale");
            sb.AppendLine();
        }
        generatedCode = sb.ToString();
    }
}
