using System.IO;
using UnityEditor;
using UnityEngine;

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
            dimensions = new Vector2(0f, 0f), // TODO: width/depth from blueprint
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: populate footprint dimensions"
        });

        // ------------------------------------------------------------------
        // 2. Covered Entry & Foyer
        plan.rooms.Add(new RoomData
        {
            roomId = "CoveredEntry",
            roomLabel = "Covered Entry",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string> { "Foyer" },
            notes = "TODO: dimensions for Covered Entry"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "Foyer",
            roomLabel = "Foyer",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string> { "CoveredEntry" },
            notes = "TODO: dimensions for Foyer"
        });

        // ------------------------------------------------------------------
        // 3. Central Hallway & Enclosed Staircase
        plan.rooms.Add(new RoomData
        {
            roomId = "CentralHallway",
            roomLabel = "Central Hallway",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Central Hallway"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "Staircase",
            roomLabel = "Enclosed Staircase",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Staircase"
        });

        // ------------------------------------------------------------------
        // 4. Bedroom → Office Wing
        plan.rooms.Add(new RoomData
        {
            roomId = "Office",
            roomLabel = "Office",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Office"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "MasterBedroom",
            roomLabel = "Master Bedroom",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Master Bedroom"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "MasterCloset",
            roomLabel = "Master Closet",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Master Closet"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "MasterVestibule",
            roomLabel = "Master Vestibule",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Master Vestibule"
        });

        // ------------------------------------------------------------------
        // 5. Bathrooms
        plan.rooms.Add(new RoomData
        {
            roomId = "HallBath",
            roomLabel = "Hall Bath",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Hall Bath"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "MasterBath",
            roomLabel = "Master Bath",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Master Bath"
        });

        // ------------------------------------------------------------------
        // 6. Living Room & Dining Room
        plan.rooms.Add(new RoomData
        {
            roomId = "LivingRoom",
            roomLabel = "Living Room",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Living Room"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "DiningRoom",
            roomLabel = "Dining Room",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: east opening should slide right-behind-left"
        });

        // ------------------------------------------------------------------
        // 7. Kitchen & Nook
        plan.rooms.Add(new RoomData
        {
            roomId = "Kitchen",
            roomLabel = "Kitchen",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Kitchen"
        });

        plan.rooms.Add(new RoomData
        {
            roomId = "Nook",
            roomLabel = "Nook",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Nook"
        });

        // ------------------------------------------------------------------
        // 8. Family Room
        plan.rooms.Add(new RoomData
        {
            roomId = "FamilyRoom",
            roomLabel = "Family Room",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Family Room"
        });

        // ------------------------------------------------------------------
        // 9. Covered Patio
        plan.rooms.Add(new RoomData
        {
            roomId = "CoveredPatio",
            roomLabel = "Covered Patio",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Covered Patio"
        });

        // ------------------------------------------------------------------
        // 10. Garage
        plan.rooms.Add(new RoomData
        {
            roomId = "Garage",
            roomLabel = "Garage",
            dimensions = new Vector2(0f, 0f), // TODO
            position = Vector3.zero,
            walls = new List<WallSegment>(),
            connectedRoomIds = new List<string>(),
            notes = "TODO: dimensions for Garage"
        });

        // ------------------------------------------------------------------
        // Placeholder door
        plan.doors.Add(new DoorSpec
        {
            doorId = "ExampleDoor",
            type = DoorType.Hinged,
            width = 0f, // TODO
            height = 0f, // TODO
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
            width = 0f, // TODO
            height = 0f, // TODO
            position = Vector3.zero,
            sillHeight = 0f, // TODO
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
            width = 0f, // TODO
            height = 0f, // TODO
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
