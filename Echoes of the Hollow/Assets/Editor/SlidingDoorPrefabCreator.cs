using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to generate a basic sliding door prefab.
/// </summary>
public static class SlidingDoorPrefabCreator
{
    [MenuItem("Tools/Create Sliding Door Prefab")]
    public static void CreateSlidingDoorPrefab()
    {
        GameObject root = new GameObject("Door_Sliding");

        GameObject leftPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPanel.name = "Panel_Left";
        leftPanel.transform.SetParent(root.transform);
        leftPanel.transform.localScale = new Vector3(0.91f, 2.03f, 0.05f);
        leftPanel.transform.localPosition = new Vector3(-0.455f, 1.015f, 0f);

        GameObject rightPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPanel.name = "Panel_Right";
        rightPanel.transform.SetParent(root.transform);
        rightPanel.transform.localScale = new Vector3(0.91f, 2.03f, 0.05f);
        rightPanel.transform.localPosition = new Vector3(0.455f, 1.015f, 0f);

        SlidingDoorController controller = root.AddComponent<SlidingDoorController>();
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("slidingPanel").objectReferenceValue = rightPanel.transform;
        so.FindProperty("slidesLeft").boolValue = true;
        so.FindProperty("slideDistance").floatValue = 0.91f;
        so.ApplyModifiedProperties();

        string prefabFolder = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        string prefabPath = prefabFolder + "/Door_Sliding.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log("Created " + prefabPath);
    }
}
