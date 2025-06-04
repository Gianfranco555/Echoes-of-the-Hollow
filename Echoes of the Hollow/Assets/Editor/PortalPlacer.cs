using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement; // Required for SceneManager

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

        // Attempt to find the script asset using its GUID
        string[] scriptAssetPaths = AssetDatabase.FindAssets($"t:MonoScript {PortalScriptName}");
        MonoScript scenePortalScriptAsset = null;

        foreach (string scriptAssetPathGuid in scriptAssetPaths) // AssetDatabase.FindAssets returns GUIDs
        {
            string path = AssetDatabase.GUIDToAssetPath(scriptAssetPathGuid);
            if (AssetDatabase.AssetPathToGUID(path) == PortalScriptGUID)
            {
                 scenePortalScriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                 if (scenePortalScriptAsset != null && scenePortalScriptAsset.GetClass() != null) // Check if class is valid
                 {
                    break;
                 }
            }
        }

        // Fallback if GUID search fails (e.g. if FindAssets by GUID isn't working as expected or script name is more reliable)
        // This part is a bit redundant if the GUID search is robust but acts as a safety.
        if (scenePortalScriptAsset == null)
        {
            scriptAssetPaths = AssetDatabase.FindAssets($"t:MonoScript {PortalScriptName}");
            foreach (string scriptAssetPathGuid in scriptAssetPaths)
            {
                string path = AssetDatabase.GUIDToAssetPath(scriptAssetPathGuid);
                MonoScript tempScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (tempScript != null && tempScript.GetClass() != null && tempScript.name == PortalScriptName)
                {
                    // To be more certain, one could also check the GUID here if available through another means,
                    // but we are in the fallback, so we assume the GUID search might have had an issue.
                    // For now, matching name is the best we can do in this fallback.
                    var guidFromPath = AssetDatabase.AssetPathToGUID(path);
                    if (guidFromPath == PortalScriptGUID) { // Double check GUID
                        scenePortalScriptAsset = tempScript;
                        break;
                    }
                    // If we don't have the GUID match, we might be picking the wrong script if names collide.
                    // But since we created the .meta with the GUID, this should ideally work.
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
