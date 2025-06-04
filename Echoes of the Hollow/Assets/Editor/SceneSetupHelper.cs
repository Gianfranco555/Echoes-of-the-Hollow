using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO; // Added for Directory.Exists
using System.Linq; // For FirstOrDefault
using Unity.AI.Navigation;

// Assuming your builder scripts (FoundationBuilder, WallBuilder, RoomBuilder, RoofBuilder, WindowPlacer)
// are in the global namespace or you have a 'using' directive for them.

public static class SceneSetupHelper
{
    private const string ScenePath = "Assets/Scenes/House_MainLevel.unity";
    private const string HousePlanPath = "Assets/BlueprintData/NewHousePlan.asset"; // Ensure this is your asset name
    private const string BasementScenePath = "Assets/Scenes/House_Basement.unity";
    private const string FoldingLadderPrefabPath = "Assets/Prefabs/FoldingLadder.prefab";
    private const string MasterClosetRoomId = "MasterCloset";
    private const string MasterBedroomRoomId = "MasterBedroom";
    private const string OfficeRoomId = "Office";


    [MenuItem("House Tools/Setup Main Level Scene (Full Build)")]
    public static void SetupMainLevelSceneWithEverything()
    {
        Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (!Directory.Exists("Assets/Scenes")) // Ensure Scenes folder exists
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        if (scene.IsValid())
        {
            bool clear = EditorUtility.DisplayDialog("Scene Exists", "House_MainLevel already exists. Clear existing contents?", "Clear", "Cancel");
            if (!clear) { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); return; }
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) { Debug.LogError($"Failed to re-open scene '{ScenePath}' for clearing."); return; }
            GameObject existingHouseRoot = GameObject.Find("ProceduralHouse_Generated");
            if (existingHouseRoot != null) Object.DestroyImmediate(existingHouseRoot);
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        var plan = AssetDatabase.LoadAssetAtPath<HousePlanSO>(HousePlanPath);
        if (plan == null) { Debug.LogError($"Failed to load HousePlanSO at {HousePlanPath}"); return; }

        GameObject houseRoot = new GameObject("ProceduralHouse_Generated");
        SceneManager.MoveGameObjectToScene(houseRoot, scene);

        float storyHeight = plan.storyHeight > 0 ? plan.storyHeight : 2.7f;

        GameObject foundationGO = FoundationBuilder.GenerateFoundation(plan);
        if (foundationGO != null) { foundationGO.name = "Foundation"; foundationGO.transform.SetParent(houseRoot.transform); }

        GameObject exteriorWallsGO = WallBuilder.GenerateExteriorWalls(plan, storyHeight);
        if (exteriorWallsGO != null) { exteriorWallsGO.name = "ExteriorWalls"; exteriorWallsGO.transform.SetParent(houseRoot.transform); }

        GameObject interiorWallsGO = RoomBuilder.GenerateInteriorWalls(plan, storyHeight);
        if (interiorWallsGO != null) { interiorWallsGO.name = "InteriorWalls"; interiorWallsGO.transform.SetParent(houseRoot.transform); }
        else { Debug.LogWarning("InteriorWallsGO was null or empty from RoomBuilder."); }


        GameObject roofGO = RoofBuilder.GenerateRoof(plan, exteriorWallsGO); // exteriorWallsGO for bounds
        if (roofGO != null) { roofGO.name = "Roof"; roofGO.transform.SetParent(houseRoot.transform); }

        // --- Place Windows ---
        Debug.Log("Attempting to place windows...");
        // We need a container that holds all wall segments.
        // If exterior and interior walls are parented directly to houseRoot, that's complex for WindowPlacer.
        // For now, let's assume windows are mostly on exterior walls.
        // Or, WindowPlacer needs to search within houseRoot for wall segments.
        // The current WindowPlacer takes a specific wallsContainer.
        if (exteriorWallsGO != null)
        {
             WindowPlacer.PlaceAllWindows(plan, exteriorWallsGO);
        }
        if (interiorWallsGO != null) // If windows can be on interior walls
        {
            // WindowPlacer.PlaceAllWindows(plan, interiorWallsGO); // Uncomment if needed and WindowPlacer handles multiple calls or combined containers
        }
        Debug.Log("Window placement call finished.");


        SetupLighting(scene);
        SetupCamera(houseRoot, plan);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Full house generation process complete.");
    }

    private static void SetupLighting(Scene scene)
    {
        // Check if a light already exists to avoid duplicates if scene wasn't fully cleared
        if (Object.FindObjectOfType<Light>() == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.956f, 0.839f); // A warmer sun color
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            SceneManager.MoveGameObjectToScene(lightObj, scene);
        }
    }

    private static void SetupCamera(GameObject housePivot, HousePlanSO plan)
    {
        Camera cam = Camera.main; // Camera.main is often more reliable
        if (cam == null)
        {
            // Try to find any camera
            cam = Object.FindAnyObjectByType<Camera>(); // Updated from FindObjectOfType
            if (cam == null) // If still no camera, create one
            {
                GameObject camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                cam.tag = "MainCamera"; // Important for Camera.main to work next time
                SceneManager.MoveGameObjectToScene(cam.gameObject, SceneManager.GetActiveScene());
            }
        }


        Bounds bounds = default;
        bool boundsInitialized = false;

        if (housePivot != null)
        {
            Renderer[] renderers = housePivot.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                boundsInitialized = true;
            }
        }

        if (!boundsInitialized && plan != null && plan.rooms != null && plan.rooms.Count > 0) // Fallback to plan bounds
        {
            // This CalculateBounds method needs to be implemented in HousePlanSO.cs
            // It should iterate through plan.rooms, use their global position and dimensions
            // to calculate an overall bounding box for the house.
            // For now, let's assume a simple fallback or use the OverallFootprint room.
            RoomData overall = plan.rooms.FirstOrDefault(r => r.roomId == "OverallFootprint");
            if (overall.roomId != null && overall.dimensions != Vector2.zero)
            {
                bounds = new Bounds(overall.position + new Vector3(overall.dimensions.x / 2, 0, overall.dimensions.y / 2),
                                    new Vector3(overall.dimensions.x, plan.storyHeight, overall.dimensions.y));
                boundsInitialized = true;
            }
        }

        if (boundsInitialized)
        {
            Vector3 target = bounds.center;
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z, 10f); // Ensure a minimum size
            cam.transform.position = target + new Vector3(-size * 0.8f, size * 0.6f, -size * 0.8f);
            cam.transform.LookAt(target);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = Mathf.Max(1000f, size * 3); // Adjust far clip plane
        }
        else
        {
            cam.transform.position = new Vector3(15, 15, -15); // Default fallback position
            cam.transform.LookAt(Vector3.zero);
            Debug.LogWarning("Could not determine house bounds for camera setup. Using default camera position.");
        }
    }

    [MenuItem("House Tools/Setup Basement Scene")]
    public static void SetupBasementScene()
    {
        Scene basementScene = EditorSceneManager.GetSceneByPath(BasementScenePath);
        if (basementScene.IsValid())
        {
            bool clear = EditorUtility.DisplayDialog(
                "Basement Scene Exists",
                $"Scene at '{BasementScenePath}' already exists. Clear existing contents?",
                "Clear", "Cancel");
            if (!clear)
            {
                EditorSceneManager.OpenScene(BasementScenePath, OpenSceneMode.Single);
                return;
            }

            basementScene = EditorSceneManager.OpenScene(BasementScenePath, OpenSceneMode.Single);
            if (!basementScene.IsValid())
            {
                Debug.LogError($"Failed to re-open scene '{BasementScenePath}' for clearing.");
                return;
            }

            // Clear all root GameObjects
            GameObject[] rootObjects = basementScene.GetRootGameObjects();
            foreach (GameObject rootObject in rootObjects)
            {
                Object.DestroyImmediate(rootObject);
            }
        }
        else
        {
            basementScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // Create a root object for all generated basement parts
        GameObject basementRoot = new GameObject("Basement_Generated");
        SceneManager.MoveGameObjectToScene(basementRoot, basementScene);

        // Define room dimensions
        float width = 5f;
        float depth = 5f;
        float height = 2.4f;

        // Create Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0, -height / 2f - 0.05f, 0); // Adjust Y to be actual floor surface
        floor.transform.localScale = new Vector3(width, 0.1f, depth);
        floor.transform.SetParent(basementRoot.transform);

        // Create Ceiling
        GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.position = new Vector3(0, height / 2f + 0.05f, 0); // Adjust Y
        ceiling.transform.localScale = new Vector3(width, 0.1f, depth);
        ceiling.transform.SetParent(basementRoot.transform);

        // Create Walls
        GameObject wallNorth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallNorth.name = "Wall_North";
        wallNorth.transform.position = new Vector3(0, 0, depth / 2f);
        wallNorth.transform.localScale = new Vector3(width, height, 0.1f);
        wallNorth.transform.SetParent(basementRoot.transform);

        GameObject wallSouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallSouth.name = "Wall_South";
        wallSouth.transform.position = new Vector3(0, 0, -depth / 2f);
        wallSouth.transform.localScale = new Vector3(width, height, 0.1f);
        wallSouth.transform.SetParent(basementRoot.transform);

        GameObject wallEast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallEast.name = "Wall_East";
        wallEast.transform.position = new Vector3(width / 2f, 0, 0);
        wallEast.transform.localScale = new Vector3(0.1f, height, depth);
        wallEast.transform.SetParent(basementRoot.transform);

        GameObject wallWest = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallWest.name = "Wall_West";
        wallWest.transform.position = new Vector3(-width / 2f, 0, 0);
        wallWest.transform.localScale = new Vector3(0.1f, height, depth);
        wallWest.transform.SetParent(basementRoot.transform);

        // --- NavMesh Generation ---
        // Collect all GameObjects for NavMesh baking
        var navMeshObjects = new System.Collections.Generic.List<GameObject>
        {
            floor, wallNorth, wallSouth, wallEast, wallWest
        };

        // Set GameObjects to be NavigationStatic
        foreach (var obj in navMeshObjects)
        {
            if (obj != null) // Ensure object exists before trying to set flags
            {
                GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.NavigationStatic);
            }
        }

        // Add NavMeshSurface component to the root
        NavMeshSurface navMeshSurface = basementRoot.AddComponent<NavMeshSurface>();

        // Bake the NavMesh
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh baked successfully.");
        }
        else
        {
            Debug.LogError("Failed to add NavMeshSurface component to basementRoot. NavMesh baking cannot proceed.");
        }
        // --- End of NavMesh Generation ---

        // Instantiate Breaker Box
        string breakerBoxPrefabPath = "Assets/Prefabs/BreakerBox.prefab";
        GameObject breakerBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(breakerBoxPrefabPath);
        if (breakerBoxPrefab != null)
        {
            // Instantiate the prefab directly into the basementScene
            GameObject breakerBoxInstance = (GameObject)PrefabUtility.InstantiatePrefab(breakerBoxPrefab, basementScene);
            if (breakerBoxInstance != null)
            {
                breakerBoxInstance.name = "BreakerBox_Instance";
                breakerBoxInstance.transform.SetParent(basementRoot.transform);
                // Position on Wall_West. Wall_West is at x = -width/2. Its thickness is 0.1.
                // Breaker box scale is (0.1f, 0.5f, 0.3f). Its X dimension is 0.1f.
                // X position: center of wall_west (-width/2) + half wall thickness (0.05f) + half breaker box depth (0.05f for X)
                // float breakerBoxXPos = -width / 2f + 0.05f + 0.05f; // Original calculation from prompt
                float breakerBoxXPos = -width / 2f + 0.1f; // Simplified from prompt, assuming it means surface + offset for center

                // Y position: floor surface is at -height/2 - 0.05f. We want bottom of breaker 1m from floor.
                // Breaker box Y scale is 0.5f, so its half-height is 0.25f.
                // Y position for center of breaker: (-height/2f - 0.05f) + 1.0f + 0.25f
                // = -1.2f - 0.05f + 1.0f + 0.25f = -0.0f
                // Let's re-evaluate from prompt: "y = -height/2 + 1.2f" (if pivot at base) or "y = 0.1f"
                // Using y = 0.1f based on final prompt calculation.
                // The comment calculation (-height/2f - 0.05f) + 1.0f + 0.25f = -1.25f + 1.25f = 0.0f.
                // Updating to reflect this calculation.
                float breakerBoxYPos = 0.0f;

                breakerBoxInstance.transform.position = new Vector3(breakerBoxXPos, breakerBoxYPos, 0);
                // Explicitly set scale for the breaker box to ensure consistent dimensions in this procedurally generated scene.
                // This overrides any scale settings defined within the BreakerBox.prefab itself.
                breakerBoxInstance.transform.localScale = new Vector3(0.1f, 0.5f, 0.3f);
                // SceneManager.MoveGameObjectToScene(breakerBoxInstance, basementScene); // No longer needed
            }
            else
            {
                Debug.LogError("Failed to instantiate BreakerBox prefab.");
            }
        }
        else
        {
            Debug.LogError($"Failed to load BreakerBox prefab at '{breakerBoxPrefabPath}'. Ensure it exists and the path is correct.");
        }

        // --- Add Point Light ---
        GameObject pointLightGameObject = new GameObject("Basement_PointLight");
        Light pointLight = pointLightGameObject.AddComponent<Light>();
        pointLight.type = LightType.Point;
        pointLight.intensity = 0.5f; // Low intensity
        pointLight.range = 10f;     // Moderate range

        // Position the light slightly below the ceiling center
        // Ceiling Y position is height / 2f + 0.05f. We use height / 2f for the light.
        pointLightGameObject.transform.position = new Vector3(0, height / 2f, 0);
        SceneManager.MoveGameObjectToScene(pointLightGameObject, basementScene); // Ensure light is in the correct scene

        // Parent to BreakerBox if found
        // Note: BreakerBox_Instance was already parented to basementRoot earlier.
        // We find it again here to attach the light to it.
        GameObject breakerBoxInstanceForLight = GameObject.Find("BreakerBox_Instance");
        if (breakerBoxInstanceForLight != null)
        {
            pointLightGameObject.transform.SetParent(breakerBoxInstanceForLight.transform);
            Debug.Log("PointLight parented to BreakerBox_Instance.");
        }
        else
        {
            // Fallback: parent to basementRoot if breaker box not found (should not happen ideally)
            pointLightGameObject.transform.SetParent(basementRoot.transform);
            Debug.LogWarning("BreakerBox_Instance not found. PointLight parented to basementRoot instead.");
        }
        Debug.Log("PointLight added to the basement scene.");

        // Subscribe PointLight to BreakerBoxController events
        if (breakerBoxInstanceForLight != null && pointLightGameObject != null)
        {
            BreakerBoxController breakerController = breakerBoxInstanceForLight.GetComponent<BreakerBoxController>();
            Light lightComponent = pointLightGameObject.GetComponent<Light>();

            if (breakerController != null && lightComponent != null)
            {
                // Subscribe to the event
                breakerController.OnPowerStateChanged += (isPoweredOn) =>
                {
                    if (lightComponent != null) // Check if lightComponent still exists
                    {
                        lightComponent.enabled = isPoweredOn;
                    }
                };
                // Initialize light state
                lightComponent.enabled = breakerController.IsPowerOn; // Use the IsPowerOn property
                Debug.Log("Subscribed PointLight to BreakerBoxController events and initialized state.");
            }
            else
            {
                if (breakerController == null) Debug.LogError("BreakerBoxController component not found on BreakerBox_Instance.");
                if (lightComponent == null) Debug.LogError("Light component not found on Basement_PointLight.");
            }
        }
        else
        {
            if (breakerBoxInstanceForLight == null) Debug.LogWarning("Cannot subscribe light to breaker: BreakerBox_Instance not found.");
            if (pointLightGameObject == null) Debug.LogWarning("Cannot subscribe light to breaker: Basement_PointLight GameObject not found.");
        }
        // --- End of Add Point Light ---

        // SetupLighting(basementScene); // Commented out to prevent adding directional light

        EditorSceneManager.MarkSceneDirty(basementScene);
        EditorSceneManager.SaveScene(basementScene, BasementScenePath);
        Debug.Log($"Basement scene setup complete at '{BasementScenePath}'.");
    }

    [MenuItem("House Tools/Setup Attic")]
    public static void SetupAttic()
    {
        var plan = AssetDatabase.LoadAssetAtPath<HousePlanSO>(HousePlanPath);
        if (plan == null)
        {
            Debug.LogError($"Failed to load HousePlanSO at {HousePlanPath}");
            return;
        }

        GameObject houseRoot = GameObject.Find("ProceduralHouse_Generated");
        if (houseRoot == null)
        {
            houseRoot = new GameObject("ProceduralHouse_Generated");
            // Ensure it's in the active scene. If no scene is open, this might be an issue.
            // It's better if SetupMainLevelScene is run first or the scene is already set up.
             if (EditorSceneManager.GetActiveScene().IsValid())
            {
                SceneManager.MoveGameObjectToScene(houseRoot, EditorSceneManager.GetActiveScene());
            }
            else
            {
                Debug.LogWarning("No active scene found for ProceduralHouse_Generated. It will be in a new unsaved scene.");
            }
        }

        GameObject atticGroup = new GameObject("Attic_Generated");
        atticGroup.transform.SetParent(houseRoot.transform);

        // --- Instantiate Folding Ladder ---
        RoomData masterCloset = plan.rooms.FirstOrDefault(r => r.roomId == MasterClosetRoomId);
        if (masterCloset.roomId == MasterClosetRoomId && masterCloset.atticHatchLocalPosition != Vector3.zero)
        {
            GameObject ladderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FoldingLadderPrefabPath);
            if (ladderPrefab != null)
            {
                // Calculate world position for the hatch.
                // atticHatchLocalPosition is relative to the room's origin.
                // RoomData.position is the world origin of the room.
                // Assumes masterCloset.atticHatchLocalPosition.y is correctly set to the ceiling height
                // relative to the room's own origin (e.g., plan.storyHeight).
                Vector3 hatchWorldPosition = masterCloset.position + masterCloset.atticHatchLocalPosition;

                GameObject ladderInstance = (GameObject)PrefabUtility.InstantiatePrefab(ladderPrefab, atticGroup.transform);
                ladderInstance.name = "FoldingAtticLadder";
                ladderInstance.transform.position = hatchWorldPosition;
                // Assuming ladder prefab is designed with Y-up, and deploys along its local -Y.
                // Hatch part is on its local XZ plane. Placed at ceiling, it deploys downwards.
                ladderInstance.transform.rotation = Quaternion.identity;
                Debug.Log($"Instantiated FoldingLadder at {hatchWorldPosition} in {MasterClosetRoomId}");
            }
            else
            {
                Debug.LogError($"Failed to load FoldingLadder prefab at {FoldingLadderPrefabPath}");
            }
        }
        else
        {
            if (masterCloset.roomId != MasterClosetRoomId)
                Debug.LogWarning($"Master Bedroom Closet with roomId '{MasterClosetRoomId}' not found in HousePlanSO.");
            else
                Debug.LogWarning($"Attic hatch local position not defined for '{MasterClosetRoomId}' or is Vector3.zero. Ladder not placed.");
        }

        // --- Procedurally Generate Attic Geometry ---
        RoomData masterBedroom = plan.rooms.FirstOrDefault(r => r.roomId == MasterBedroomRoomId);
        RoomData office = plan.rooms.FirstOrDefault(r => r.roomId == OfficeRoomId);

        if (masterBedroom.roomId == null || office.roomId == null)
        {
            Debug.LogError("Master Bedroom or Office not found in HousePlanSO. Cannot generate attic geometry.");
        }
        else
        {
            // Calculate combined bounds of Master Bedroom and Office
            // Assuming room.position is bottom-corner, so center for bounds calculation needs offset.
            Vector3 masterBedroomCenter = masterBedroom.position + new Vector3(masterBedroom.dimensions.x / 2, plan.storyHeight / 2, masterBedroom.dimensions.y / 2);
            Bounds combinedBounds = new Bounds(masterBedroomCenter,
                                             new Vector3(masterBedroom.dimensions.x, plan.storyHeight, masterBedroom.dimensions.y));

            Vector3 officeCenter = office.position + new Vector3(office.dimensions.x / 2, plan.storyHeight / 2, office.dimensions.y / 2);
            combinedBounds.Encapsulate(new Bounds(officeCenter,
                                             new Vector3(office.dimensions.x, plan.storyHeight, office.dimensions.y)));

            float atticFloorThickness = 0.2f;
            float atticWallHeight = 1.5f; // Low attic ceiling
            float roofThickness = 0.3f;

            // Create Attic Floor
            GameObject atticFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            atticFloor.name = "AtticFloor";
            atticFloor.transform.SetParent(atticGroup.transform);
            // The combinedBounds.center.y is at plan.storyHeight / 2. We want the floor top at plan.storyHeight.
            // So bottom of attic floor is at plan.storyHeight. Center is plan.storyHeight + thickness/2.
            atticFloor.transform.position = new Vector3(combinedBounds.center.x, plan.storyHeight + atticFloorThickness / 2, combinedBounds.center.z);
            atticFloor.transform.localScale = new Vector3(combinedBounds.size.x + 0.5f, atticFloorThickness, combinedBounds.size.z + 0.5f); // Slightly larger floor

            // Create simple Attic Walls
            GameObject atticWalls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            atticWalls.name = "AtticBoundary";
            atticWalls.transform.SetParent(atticGroup.transform);
            atticWalls.transform.position = new Vector3(combinedBounds.center.x,
                                                       plan.storyHeight + atticFloorThickness + atticWallHeight / 2,
                                                       combinedBounds.center.z);
            atticWalls.transform.localScale = new Vector3(combinedBounds.size.x, atticWallHeight, combinedBounds.size.z);

            // Create a simple flat ceiling placeholder
            GameObject atticCeiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            atticCeiling.name = "AtticCeiling";
            atticCeiling.transform.SetParent(atticGroup.transform);
            atticCeiling.transform.position = new Vector3(combinedBounds.center.x,
                                                          plan.storyHeight + atticFloorThickness + atticWallHeight + roofThickness / 2,
                                                          combinedBounds.center.z);
            atticCeiling.transform.localScale = new Vector3(combinedBounds.size.x + 0.5f, roofThickness, combinedBounds.size.z + 0.5f);

            Debug.Log($"Generated placeholder attic geometry above {MasterBedroomRoomId} and {OfficeRoomId}.");
        }

        if (EditorSceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Attic setup complete. Remember to save the scene.");
        }
        else
        {
            Debug.LogWarning("Attic setup processed, but no valid scene was active to mark as dirty.");
        }
    }
}
