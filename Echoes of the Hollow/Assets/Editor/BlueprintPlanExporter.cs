using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu utilities for exporting <see cref="HousePlanSO"/> assets to JSON.
/// </summary>
public static class BlueprintPlanExporter
{
    [MenuItem("House Tools/Export HousePlan to JSON")]
    private static void ExportSelectedPlan()
    {
        var plan = Selection.activeObject as HousePlanSO;
        if (plan == null)
        {
            Debug.LogError("Please select a HousePlanSO asset to export.");
            return;
        }

        string directory = Path.Combine("Assets", "StreamingAssets");
        if (!AssetDatabase.IsValidFolder(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Export HousePlan to JSON",
            plan.name + ".json",
            "json",
            "Choose a location within StreamingAssets",
            directory);

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        BlueprintJsonWriter.Write(plan, path);
        Debug.Log($"HousePlan exported to {path}");
    }
}
