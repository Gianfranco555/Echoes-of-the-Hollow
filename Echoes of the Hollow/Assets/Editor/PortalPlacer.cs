using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;// Required for SceneManager

public static class PortalPlacer
{
    private const string MainLevelScenePath = "Assets/Scenes/House_MainLevel.unity";
    private const string PortalScriptName = "ScenePortal"; // Just the name, not ScenePortal.cs
    private const string PortalScriptGUID = "fcb8802b-bf68-4219-b7d5-11c559d89107"; // GUID generated for ScenePortal.cs.meta

    [MenuItem("House Tools/Place Basement Portal in Main Level")]
    public static void PlacePortalInMainLevel()
    {
        // Attempt to open the scene
        Scene mainLevelScene = EditorSceneManager.OpenScene(MainLevelScenePath, OpenSceneMode.Single);

        if (!mainLevelScene.IsValid())
        {
            Debug.LogError($"Failed to load scene '{MainLevelScenePath}'. Ensure the scene exists at this path.");
            return;
        }

        // Create Portal GameObject
        GameObject portalGO = new GameObject("Basement_Portal");
        SceneManager.MoveGameObjectToScene(portalGO, mainLevelScene); // Ensure it's in the correct scene

        portalGO.transform.position = new Vector3(0, 0.5f, 0);

        // Add BoxCollider
        BoxCollider collider = portalGO.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1, 2, 1);

        MonoScript scenePortalScriptAsset = null;

        // Attempt 1: Direct GUID Load
        string scriptPathByGuid = AssetDatabase.GUIDToAssetPath(PortalScriptGUID);
        if (!string.IsNullOrEmpty(scriptPathByGuid))
        {
            scenePortalScriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPathByGuid);
            // Verify that the loaded asset is indeed a valid script with a class
            if (scenePortalScriptAsset != null && scenePortalScriptAsset.GetClass() == null)
            {
                scenePortalScriptAsset = null; // Invalid script, treat as not loaded
            }
        }

        // Attempt 2: Fallback to Name Search + GUID Check
        if (scenePortalScriptAsset == null)
        {
            string[] scriptAssetPathGuidsByName = AssetDatabase.FindAssets($"t:MonoScript {PortalScriptName}");
            foreach (string guidInList in scriptAssetPathGuidsByName)
            {
                string pathByName = AssetDatabase.GUIDToAssetPath(guidInList);
                MonoScript tempScript = AssetDatabase.LoadAssetAtPath<MonoScript>(pathByName);

                // Check if the script name matches and, crucially, if its GUID matches the expected PortalScriptGUID
                if (tempScript != null && tempScript.GetClass() != null && tempScript.name == PortalScriptName &&
                    AssetDatabase.AssetPathToGUID(pathByName) == PortalScriptGUID)
                {
                    scenePortalScriptAsset = tempScript;
                    break; // Found the correct script
                }
            }
        }

        if (scenePortalScriptAsset == null || scenePortalScriptAsset.GetClass() == null)
        {
            Debug.LogError($"Failed to find the {PortalScriptName}.cs script asset. Ensure it exists in the project, has compiled, and its .meta file with GUID {PortalScriptGUID} is present.");
            Object.DestroyImmediate(portalGO); // Clean up the created GameObject
            return;
        }

        // Add ScenePortal script component
        Component portalComponent = portalGO.AddComponent(scenePortalScriptAsset.GetClass());
        ScenePortal scenePortalInstance = portalComponent as ScenePortal;

        if (scenePortalInstance == null)
        {
            Debug.LogError($"Failed to add or cast {PortalScriptName} component to the GameObject. The script asset might be invalid or not derive from MonoBehaviour.");
            Object.DestroyImmediate(portalGO); // Clean up
            return;
        }

        // Configure ScenePortal component
        scenePortalInstance.targetSceneName = "House_Basement"; // As per instruction, name without .unity
        scenePortalInstance.targetSpawnPointId = 0;
        scenePortalInstance.unloadCurrentScene = true;

        // Save Changes
        EditorSceneManager.MarkSceneDirty(mainLevelScene);
        EditorSceneManager.SaveScene(mainLevelScene);

        Debug.Log($"'Basement_Portal' GameObject with ScenePortal script added to '{MainLevelScenePath}' and scene saved.");
    }
}
