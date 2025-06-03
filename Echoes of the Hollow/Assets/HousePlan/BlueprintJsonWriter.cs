using System.IO;
using UnityEngine;

/// <summary>
/// Utility for writing <see cref="HousePlanSO"/> data to a JSON file.
/// </summary>
public static class BlueprintJsonWriter
{
    /// <summary>
    /// Serialises the given <see cref="HousePlanSO"/> to the specified JSON path.
    /// </summary>
    /// <param name="plan">House plan to serialise.</param>
    /// <param name="pathJson">Path to the output JSON file.</param>
    public static void Write(HousePlanSO plan, string pathJson)
    {
        if (plan == null || string.IsNullOrEmpty(pathJson))
        {
            Debug.LogError("Invalid arguments supplied to BlueprintJsonWriter.Write.");
            return;
        }

        string json = JsonUtility.ToJson(plan, true);
        string directory = Path.GetDirectoryName(pathJson);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(pathJson, json);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}
