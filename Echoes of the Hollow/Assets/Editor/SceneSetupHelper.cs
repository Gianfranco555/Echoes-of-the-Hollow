using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
// Make sure you have a using statement for your builder scripts if they are in a different namespace
// using YourProject.Builders; // Example if you used namespaces

/// <summary>
/// Editor utilities for setting up the main level scene based on a HousePlanSO asset.
/// </summary>
public static class SceneSetupHelper // It's good practice for MenuItem classes to be static or for methods to be static
{
    private const string ScenePath = "Assets/Scenes/House_MainLevel.unity";
    private const string HousePlanPath = "Assets/BlueprintData/NewHousePlan.asset"; // This seems correct for your asset

    [MenuItem("House Tools/Setup Main Level Scene")] // This menu item will now build more
    // It's best practice for MenuItem methods to be public, though private can sometimes work.
    public static void SetupMainLevelScene() // Changed to public
    {
        Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid())
        {
            bool clear = EditorUtility.DisplayDialog(
                "Scene Exists",
                "House_MainLevel already exists. Clear existing contents?",
                "Clear", "Cancel");
            if (!clear)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"Failed to re-open scene '{ScenePath}' for clearing.");
                return;
            }

            // Clear existing specific house root object instead of all root objects
            // This assumes you parent everything under a main "ProceduralHouse_Generated" GameObject
            GameObject existingHouseRoot = GameObject.Find("ProceduralHouse_Generated"); // Choose a consistent root name
            if (existingHouseRoot != null)
            {
                Object.DestroyImmediate(existingHouseRoot);
            }
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Scene will be saved at the end
        }

        var plan = AssetDatabase.LoadAssetAtPath<HousePlanSO>(HousePlanPath);
        if (plan == null)
        {
            Debug.LogError($"Failed to load HousePlanSO at {HousePlanPath}");
            return;
        }

        // Create a root object for all generated house parts
        GameObject houseRoot = new GameObject("ProceduralHouse_Generated");
        SceneManager.MoveGameObjectToScene(houseRoot, scene); // Move root to the correct scene

        // --- Generate Foundation ---
        // Assuming FoundationBuilder methods are static or you have an instance
        GameObject foundationGO = FoundationBuilder.GenerateFoundation(plan);
        if (foundationGO != null)
        {
            foundationGO.name = "Foundation";
            foundationGO.transform.SetParent(houseRoot.transform); // Parent to houseRoot
            // No need to MoveGameObjectToScene if parent is already in the scene
        }

        // --- ADDED: Generate Exterior Walls ---
        // Assuming WallBuilder methods are static or you have an instance
        // And that you have a WallBuilder.cs script
        float storyHeight = plan.storyHeight > 0 ? plan.storyHeight : 2.7f; // Get story height
        GameObject exteriorWallsGO = WallBuilder.GenerateExteriorWalls(plan, storyHeight);
        if (exteriorWallsGO != null)
        {
            exteriorWallsGO.name = "ExteriorWalls";
            exteriorWallsGO.transform.SetParent(houseRoot.transform); // Parent to houseRoot
        }

        Debug.Log("Attempting to generate interior walls..."); // For checking console
        GameObject interiorWallsGO = RoomBuilder.GenerateInteriorWalls(plan, storyHeight);
        if (interiorWallsGO != null && interiorWallsGO.transform.childCount > 0)
        {
            interiorWallsGO.name = "InteriorWalls";
            interiorWallsGO.transform.SetParent(houseRoot.transform);
            Debug.Log("Interior walls generated and parented.");
        }
        else
        {
            Debug.LogWarning("InteriorWallsGO was null or empty. Check RoomBuilder logic and HousePlanSO data for interior walls.");
            if(interiorWallsGO != null) Object.DestroyImmediate(interiorWallsGO); // Clean up empty parent if it was created
        }
        // --- >>> END OF SECTION FOR INTERIOR WALLS <<< ---

        // --- Generate Placeholder Roof ---
        // ... (your existing code for the roof) ...
        if (roofGO != null)
        {
            roofGO.name = "PlaceholderRoof"; // Or "ActualRoof" if you prefer
            roofGO.transform.SetParent(houseRoot.transform); // Parent to houseRoot
        }

        // --- ADDED: Generate Placeholder Roof ---
        // Assuming RoofBuilder methods are static or you have an instance
        // And that you have a RoofBuilder.cs script
        GameObject roofGO = RoofBuilder.GenerateRoof(plan, exteriorWallsGO);
        if (roofGO != null)
        {
            roofGO.name = "PlaceholderRoof";
            roofGO.transform.SetParent(houseRoot.transform); // Parent to houseRoot
        }

        // (Later, you will add calls for Interior Walls, Windows, Doors, etc. here)

        SetupLighting(scene); // Pass the active scene
        SetupCamera(houseRoot, plan); // Pass houseRoot or foundation for better camera targeting

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath); // Ensure scene is saved with the new path if it was new
        Debug.Log("House generation process (Foundation, Walls, Roof) complete.");
    }

    // ... (SetupLighting method remains the same) ...
    private static void SetupLighting(Scene scene)
    {
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.956f, 0.839f);
        light.intensity = 1f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        SceneManager.MoveGameObjectToScene(lightObj, scene); // Ensure light is moved to the correct scene
    }


    // Modified SetupCamera to potentially target the houseRoot
    private static void SetupCamera(GameObject housePivot, HousePlanSO plan) // Changed first parameter
    {
        Camera cam = Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            // If creating a new camera, ensure it's moved to the active scene
            SceneManager.MoveGameObjectToScene(cam.gameObject, SceneManager.GetActiveScene());
        }

        Bounds bounds;
        if (housePivot != null) // Try to get bounds from all children of housePivot if possible
        {
            Renderer[] renderers = housePivot.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            else // Fallback to plan bounds if no renderers
            {
                bounds = plan.CalculateBounds();
            }
        }
        else // Fallback to plan bounds if no housePivot
        {
            bounds = plan.CalculateBounds();
        }
        
        Vector3 target = bounds.center;
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 5f); // Ensure size is not too small
        // Adjust camera distance based on overall size
        cam.transform.position = target + new Vector3(-size * 0.75f, size * 0.75f, -size * 0.75f);
        cam.transform.LookAt(target);
    }
}
