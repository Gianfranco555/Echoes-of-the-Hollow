using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic; // Add this line

/// <summary>
/// Utilities for creating HousePlanSO assets from blueprint data.
/// </summary>
public static class BlueprintImporter
{
    /// <summary>
    /// Creates a new HousePlanSO asset under Assets/BlueprintData/ at the given path.
    /// </summary>
    /// <param name="path">File name for the asset relative to Assets/BlueprintData/.</param>
    public static void CreateHousePlanAsset(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Invalid path provided for HousePlanSO asset.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/BlueprintData"))
        {
            AssetDatabase.CreateFolder("Assets", "BlueprintData");
        }

        var plan = ScriptableObject.CreateInstance<HousePlanSO>();
        PopulateDataFromBlueprint(plan);

        string assetPath = Path.Combine("Assets/BlueprintData", path);
        AssetDatabase.CreateAsset(plan, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"HousePlan asset created at {assetPath}");
    }

    /// <summary>
    /// Populates the HousePlanSO with data parsed from a blueprint.
    /// </summary>
    /// <param name="plan">The plan instance to populate.</param>
    private static void PopulateDataFromBlueprint(HousePlanSO plan)
    {
        if (plan == null)
        {
            Debug.LogError("HousePlanSO is null in PopulateDataFromBlueprint.");
            return;
        }

        plan.rooms = new List<RoomData>();
        plan.doors = new List<DoorSpec>();
        plan.windows = new List<WindowSpec>();
        plan.openings = new List<OpeningSpec>();

        // The blueprint text referenced in project documentation is unavailable
        // in this repository. To keep the project functional, we create
        // placeholder entries for each required section. Replace all TODO values
        // with actual measurements and wall data once the blueprint details are
        // provided.

        // ------------------------------------------------------------------
       // 1. Overall Footprint & Circulation
        plan.rooms.Add(new RoomData
        {
            roomId = "OverallFootprint",
            roomLabel = "Overall Footprint",
            dimensions = new Vector2(30f, 51f), // TODO: width/depth from blueprint -> Updated: Approx. 30ft E-W, 51ft N-S
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "Calculated overall width (30ft) and depth (51ft) based on room layout and blueprint descriptions." // TODO: populate footprint dimensions -> Updated
        });

        // ------------------------------------------------------------------
        // 2. Covered Entry & Foyer
        plan.rooms.Add(new RoomData
        {
            roomId = "CoveredEntry",
            roomLabel = "Covered Entry",
            dimensions = new Vector2(6f, 3f), // TODO -> Updated: 6ft wide (E-W), 3ft deep (N-S)
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string> { "Foyer" },
            notes = "Dimensions for Covered Entry from blueprint: 6ft wide by 3ft deep." // TODO: dimensions for Covered Entry -> Updated
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "Foyer",
            roomLabel = "Foyer",
            dimensions = new Vector2(6f, 6f), // TODO -> Updated: 6ft x 6ft
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string> { "CoveredEntry", "LivingRoom", "Garage", "CentralHallway" }, // Added potential connections
            notes = "Dimensions for Foyer from blueprint: 6ft x 6ft." // TODO: dimensions for Foyer -> Updated
        });

        // ------------------------------------------------------------------
        // 3. Central Hallway & Enclosed Staircase
        plan.rooms.Add(new RoomData
        {
            roomId = "CentralHallway",
            roomLabel = "Central Hallway",
            dimensions = new Vector2(3f, 22f), // TODO -> Updated: 3ft wide, approx. 22ft long
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string> { "Foyer", "SecondaryBedroom", "HallBath", "MasterVestibule", "FamilyRoom", "Staircase" }, // Added potential connections
            notes = "Dimensions for Central Hallway: 3ft wide (blueprint), length approx. 22ft (derived from adjacent N-S room depths)." // TODO: dimensions for Central Hallway -> Updated
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "Staircase",
            roomLabel = "Enclosed Staircase",
            dimensions = new Vector2(3.5f, 12f), // TODO -> Updated: Approx. 3.5ft wide, 12ft long (N-S)
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string> { "Foyer", "CentralHallway", "DiningRoom" }, // Added potential connections
            notes = "Estimated dimensions for Staircase: approx. 3.5ft wide and 12ft long to descend one level." // TODO: dimensions for Staircase -> Updated
        });

        // ------------------------------------------------------------------
    plan.rooms.Add(new RoomData
    {
        roomId = "Office", // Corresponds to "Secondary Bedroom" in the blueprint
        roomLabel = "Office",
        dimensions = new Vector2(9.166667f, 10f), // 9' 2" (E-W) × 10' (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Office (Secondary Bedroom from blueprint)"
    });

    plan.rooms.Add(new RoomData
    {
        roomId = "MasterBedroom",
        roomLabel = "Master Bedroom",
        dimensions = new Vector2(11f, 12f), // 11' (E-W) × 12' (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Master Bedroom"
    });

    plan.rooms.Add(new RoomData
    {
        roomId = "MasterCloset",
        roomLabel = "Master Closet",
        dimensions = new Vector2(2f, 6f), // Assumed 2' deep (E-W) × 6' long (N-S) along the wall
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Master Closet (approx. 6ft span, assumed 2ft depth)"
    });

    plan.rooms.Add(new RoomData
    {
        roomId = "MasterVestibule",
        roomLabel = "Master Vestibule",
        dimensions = new Vector2(4f, 3f), // 4' wide (E-W) by 3' deep (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Master Vestibule"
    });

    // ------------------------------------------------------------------
    // 5. Bathrooms
    plan.rooms.Add(new RoomData
    {
        roomId = "HallBath",
        roomLabel = "Hall Bath",
        dimensions = new Vector2(5f, 9f), // 5' (E-W) × 9' (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Hall Bath"
    });

    plan.rooms.Add(new RoomData
    {
        roomId = "MasterBath",
        roomLabel = "Master Bath",
        dimensions = new Vector2(6f, 8.5f), // 6' (E-W) × 8'-9' (N-S), used 8.5'
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Master Bath"
    });

    // ------------------------------------------------------------------
    // 6. Living Room & Dining Room
    plan.rooms.Add(new RoomData
    {
        roomId = "LivingRoom",
        roomLabel = "Living Room",
        dimensions = new Vector2(12.666667f, 15f), // 12' 8" (E-W) × 15' (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Living Room"
    });

    plan.rooms.Add(new RoomData
    {
        roomId = "DiningRoom",
        roomLabel = "Dining Room",
        dimensions = new Vector2(9.333333f, 10.333333f), // 9' 4" (E-W) × 10' 4" (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Dining Room; east opening should slide right-behind-left"
    });

    // ------------------------------------------------------------------
    // 7. Kitchen & Nook
    plan.rooms.Add(new RoomData
    {
        roomId = "Kitchen",
        roomLabel = "Kitchen",
        dimensions = new Vector2(10f, 9f), // Approx. 10' (E-W) by 9' (N-S) working space
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Kitchen"
    });

    plan.rooms.Add(new RoomData
    {
        roomId = "Nook",
        roomLabel = "Nook",
        dimensions = new Vector2(6f, 7f), // Approx. 6' (E-W) × 7' (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Nook"
    });

    // ------------------------------------------------------------------
    // 8. Family Room
    plan.rooms.Add(new RoomData
    {
        roomId = "FamilyRoom",
        roomLabel = "Family Room",
        dimensions = new Vector2(12.333333f, 15.5f), // 12' 4" (E-W) × 15' 6" (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Family Room"
    });

    // ------------------------------------------------------------------
    // 9. Covered Patio
    plan.rooms.Add(new RoomData
    {
        roomId = "CoveredPatio",
        roomLabel = "Covered Patio",
        dimensions = new Vector2(6f, 6f), // Approx. 6' × 6'
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Covered Patio"
    });

    // ------------------------------------------------------------------
    // 10. Garage
    plan.rooms.Add(new RoomData
    {
        roomId = "Garage",
        roomLabel = "Garage",
        dimensions = new Vector2(11.333333f, 20f), // 11' 4" (E-W) × 20' (N-S)
        position = Vector3.zero,
        walls = new List<WallSegment>(),
        connectedRoomIds = new List<string>(),
        notes = "Dimensions for Garage"
    });

        // ------------------------------------------------------------------
        // Placeholder door
        plan.doors.Add(new DoorSpec
    {
        doorId = "ExampleDoor",
        type = DoorType.Hinged,
        width = 2.666667f, // Standard 2' 8"
        height = 6.666667f, // Standard 6' 8"
        position = Vector3.zero,
        wallId = string.Empty,
        swingDirection = SwingDirection.InwardNorth,
        slideDirection = SlideDirection.SlidesLeft,
        isExterior = false,
        connectsRoomA_Id = string.Empty,
        connectsRoomB_Id = string.Empty
    });

    // Placeholder window
    plan.windows.Add(new WindowSpec
    {
        windowId = "ExampleWindow",
        type = WindowType.SingleHung,
        width = 3.0f, // Common standard window width
        height = 4.0f, // Common standard window height
        position = Vector3.zero,
        sillHeight = 0f, // TODO: Sill height varies, not specified in general terms
        wallId = string.Empty,
        isOperable = true,
        bayPanes = 0,
        bayProjectionDepth = 0f
    });

    // Placeholder opening (Master closet attic hatch)
    plan.openings.Add(new OpeningSpec
    {
        openingId = "AtticHatch01",
        type = OpeningType.CasedOpening,
        width = 2.0f, // Common attic hatch size (24 inches)
        height = 2.0f, // Common attic hatch size (24 inches)
        position = Vector3.zero,
        wallId = string.Empty,
        passthroughLedgeDepth = 0f,
        connectsRoomA_Id = string.Empty,
        connectsRoomB_Id = string.Empty
    });
}

    [MenuItem("House Tools/Create House Plan from Blueprint")]
    private static void CreateHousePlanMenuItem()
    {
        string defaultName = "NewHousePlan.asset";
        string directory = "Assets/BlueprintData";
        string fullPath = EditorUtility.SaveFilePanelInProject(
            "Create House Plan",
            Path.GetFileNameWithoutExtension(defaultName),
            "asset",
            "Specify where to save the HousePlan asset.",
            directory);

        if (!string.IsNullOrEmpty(fullPath))
        {
            string fileName = Path.GetFileName(fullPath);
            CreateHousePlanAsset(fileName);
        }
    }
}
