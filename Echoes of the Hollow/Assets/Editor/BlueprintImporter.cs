using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic; // Add this line

/// <summary>
/// Utilities for creating HousePlanSO assets from blueprint data.
/// </summary>
public static class BlueprintImporter
{
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
    /// Creates a new HousePlanSO asset under Assets/BlueprintData/ at the given path.
    /// </summary>
    /// <param name="path">File name for the asset relative to Assets/BlueprintData/.</param>
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

    // --- Define Room Dimensions (feet) ---
    // Garage
    float garageWidth = 11f + 4f/12f; // 11.333333f
    float garageDepth = 20f;
    // Covered Entry
    float coveredEntryWidth = 6f;
    float coveredEntryDepth = 3f;
    // Foyer
    float foyerWidth = 6f;
    float foyerDepth = 6f;
    // Living Room
    float lrWidth = 12f + 8f/12f; // 12.666667f
    float lrDepth = 15f;
    // Office (Secondary Bedroom)
    float officeWidth = 9f + 2f/12f; // 9.166667f
    float officeDepth = 10f;
    // Central Hallway (estimate, may need refinement based on final layout)
    float hallWidth = 3f;
    // Hallway length needs to connect Foyer to Family Room & Master Vestibule area.
    // Estimate: Office(10) + HallBath(9) + MasterVestibule_approach(3) = 22 approx.
    float hallDepthEstimate = 22f;
    // Staircase
    float stairWidth = 3.5f;
    float stairDepthEstimate = 12f; // For a single level descent
    // Hall Bath
    float hallBathWidth = 5f;
    float hallBathDepth = 9f;
    // Master Bedroom
    float mbWidth = 11f;
    float mbDepth = 12f;
    // Master Closet (as a room)
    float mcWidth = 2f; // Depth into wall
    float mcDepth = 6f; // Length along wall
    // Master Vestibule
    float mvWidth = 4f;
    float mvDepth = 3f;
    // Master Bath
    float masterBathWidth = 6f;
    float masterBathDepth = 8.5f;
    // Dining Room
    float drWidth = 9f + 4f/12f; // 9.333333f
    float drDepth = 10f + 4f/12f; // 10.333333f
    // Kitchen
    float kWidth = 10f;
    float kDepth = 9f;
    // Nook
    float nookWidth = 6f;
    float nookDepth = 7f;
    // Family Room
    float frWidth = 12f + 4f/12f; // 12.333333f
    float frDepth = 15f + 6f/12f; // 15.5f
    // Covered Patio
    float cpWidth = 6f; // E-W depth from house
    float cpDepth = 6f; // N-S length along house

    // --- Calculate Global Positions (SW corner of each room) ---
    // Origin: Garage SW corner at (0,0,0)
    Vector3 garagePos = Vector3.zero;

    // Foyer: East of Garage
    Vector3 foyerPos = new Vector3(garagePos.x + garageWidth, garagePos.z, 0);

    // Covered Entry: South of Foyer, centered with Foyer
    Vector3 coveredEntryPos = new Vector3(foyerPos.x + (foyerWidth - coveredEntryWidth) / 2f, 0, foyerPos.z - coveredEntryDepth);

    // Living Room: East of Foyer
    Vector3 lrPos = new Vector3(foyerPos.x + foyerWidth, foyerPos.z, 0);
    float houseEastmostX = lrPos.x + lrWidth; // Define the East extent of the house

    // Office (Secondary Bedroom): North of Garage
    Vector3 officePos = new Vector3(garagePos.x, 0, garagePos.z + garageDepth);

    // Hall Bath: North of Office. Assuming it aligns with Office's West (X=0)
    Vector3 hallBathPos = new Vector3(officePos.x, 0, officePos.z + officeDepth);

    // Master Bedroom: North of Hall Bath. NW corner of house.
    Vector3 mbPos = new Vector3(hallBathPos.x, 0, hallBathPos.z + hallBathDepth);
    float houseNorthmostZ = mbPos.z + mbDepth; // Define North extent (West wing)

    // Central Hallway: North of Foyer, West of Staircase.
    // Its West edge aligns with Foyer's West edge (which is Garage's East edge).
    Vector3 hallPos = new Vector3(foyerPos.x, 0, foyerPos.z + foyerDepth);

    // Staircase: East of Central Hallway's initial segment.
    Vector3 stairPos = new Vector3(hallPos.x + hallWidth, 0, hallPos.z);

    // Master Vestibule: South of Master Bedroom's door, often aligned with Hallway extension.
    // MB South wall is at mbPos.z. Vestibule is 3ft deep (N-S).
    // MB is 11ft wide. Vestibule 4ft wide (E-W). "East side opens to small interior vestibule".
    // If MB door is on South wall, Vestibule is North of that.
    // Blueprint 4.3: MB Door (South Wall) opens South into Vestibule. Vestibule is ~3ft deep (N-S) x 4ft wide (E-W).
    // If MB is X=[0,11], Z=[mbPos.z, mbPos.z+mbDepth]. South wall at Z=mbPos.z.
    // Vestibule is South of this. So Vestibule North edge is mbPos.z.
    // Vestibule position: SW corner. If it's on the East side of MB's south access.
    // Example: MB X=[0,11]. Vestibule X=[11-4, 11] = [7,11]. Z=[mbPos.z-mvDepth, mbPos.z].
    Vector3 mvPos = new Vector3(mbPos.x + mbWidth - mvWidth, 0, mbPos.z - mvDepth);

    // Master Bath: South of Vestibule.
    // Vestibule X=[7,11], Z=[mvPos.z, mvPos.z+mvDepth]. South edge at Z=mvPos.z.
    // MBath is 6ft wide. If aligned with East edge of Vestibule: X = [11-6, 11] = [5,11].
    Vector3 masterBathPos = new Vector3(mvPos.x + mvWidth - masterBathWidth, 0, mvPos.z - masterBathDepth);

    // Master Closet: User defined as a room. "East wall of Master Bedroom". 6ft long (N-S).
    // If MB is X=[0,11]. Closet on East wall (X=11). Let's say centered on MB's depth.
    // MB Z=[mbPos.z, mbPos.z+mbDepth]. Mid-Z = mbPos.z + (mbDepth-mcDepth)/2.
    Vector3 mcPos = new Vector3(mbPos.x + mbWidth, 0, mbPos.z + (mbDepth - mcDepth) / 2f);

    // Dining Room: North of Living Room, East of Staircase.
    // LR North edge at Z=lrDepth. Staircase East edge at X = stairPos.x + stairWidth.
    Vector3 drPos = new Vector3(stairPos.x + stairWidth, 0, lrPos.z + lrDepth);

    // Kitchen: North of Dining Room. "Center-East". Approx 10'W x 9'D.
    // Let its East edge align with houseEastmostX.
    Vector3 kPos = new Vector3(houseEastmostX - kWidth, 0, drPos.z + drDepth);

    // Nook: North of Kitchen. NE Corner. Approx 6'W x 7'D.
    // Let its East edge align with houseEastmostX.
    Vector3 nookPos = new Vector3(houseEastmostX - nookWidth, 0, kPos.z + kDepth);
    float eastWingNorthmostZ = nookPos.z + nookDepth; // Define North extent (East wing)

    // Family Room: North-Central. West of Nook/Kitchen, East of Master Bedroom area.
    // 12'4"W x 15'6"D.
    // Its East edge aligns with Kitchen's West edge (kPos.x).
    // Its North edge should be a primary facade, potentially aligning with nookNorthmostZ or houseNorthmostZ.
    // Let's align its North wall with Nook's North wall.
    Vector3 frPos = new Vector3(kPos.x - frWidth, 0, eastWingNorthmostZ - frDepth);

    // Covered Patio: NE area. 6'x6'. East of Kitchen/Nook doors.
    // Its West edge is houseEastmostX. Extends 6ft East.
    // Aligns with Nook's lower Z range.
    Vector3 cpPos = new Vector3(houseEastmostX, 0, nookPos.z);


    // --- Room Definitions ---

    plan.rooms.Add(new RoomData {
        roomId = "OverallFootprint", roomLabel = "Overall Footprint",
        dimensions = new Vector2(houseEastmostX, Mathf.Max(houseNorthmostZ, eastWingNorthmostZ)),
        position = Vector3.zero, // Conceptual, covers everything
        walls = new List<WallSegment>(), connectedRoomIds = new List<string>(),
        notes = "Overall house footprint."
    });

    plan.rooms.Add(new RoomData { // Garage
        roomId = "Garage", roomLabel = "Garage", dimensions = new Vector2(garageWidth, garageDepth),
        position = garagePos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, garageDepth), endPoint = new Vector3(garageWidth, 0, garageDepth), isExterior = false }, // N (to Office)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0, 0, 6f), isExterior = true }, // W (S of OHD)
            new WallSegment { startPoint = new Vector3(0, 0, 14f), endPoint = new Vector3(0, 0, garageDepth), isExterior = true }, // W (N of OHD)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(1f, 0, 0), isExterior = true }, // S (W of Ped Door)
            new WallSegment { startPoint = new Vector3(3.667f, 0, 0), endPoint = new Vector3(garageWidth, 0, 0), isExterior = true }, // S (E of Ped Door)
            new WallSegment { startPoint = new Vector3(garageWidth, 0, 0), endPoint = new Vector3(garageWidth, 0, 8.6665f), isExterior = false }, // E (S of Foyer Door)
            new WallSegment { startPoint = new Vector3(garageWidth, 0, 11.3335f), endPoint = new Vector3(garageWidth, 0, garageDepth), isExterior = false } // E (N of Foyer Door)
        },
        connectedRoomIds = new List<string> { "Foyer", "Office" },
        notes = "South-West corner of the house."
    });

    plan.rooms.Add(new RoomData { // Covered Entry
        roomId = "CoveredEntry", roomLabel = "Covered Entry", dimensions = new Vector2(coveredEntryWidth, coveredEntryDepth),
        position = coveredEntryPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, coveredEntryDepth), endPoint = new Vector3(coveredEntryWidth, 0, coveredEntryDepth), isExterior = true }, // N (against Foyer)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0, 0, coveredEntryDepth), isExterior = true }, // W
            new WallSegment { startPoint = new Vector3(coveredEntryWidth, 0, 0), endPoint = new Vector3(coveredEntryWidth, 0, coveredEntryDepth), isExterior = true }  // E
        },
        connectedRoomIds = new List<string> { "Foyer" },
        notes = "Exterior, South of Foyer."
    });

    plan.rooms.Add(new RoomData { // Foyer
        roomId = "Foyer", roomLabel = "Foyer", dimensions = new Vector2(foyerWidth, foyerDepth),
        position = foyerPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(1.5f, 0, 0), isExterior = true }, // S (W of Front Door)
            new WallSegment { startPoint = new Vector3(4.5f, 0, 0), endPoint = new Vector3(foyerWidth, 0, 0), isExterior = true }, // S (E of Front Door)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0, 0, 1.6665f), isExterior = false }, // W (S of Garage Door)
            new WallSegment { startPoint = new Vector3(0, 0, 4.3335f), endPoint = new Vector3(0, 0, foyerDepth), isExterior = false }, // W (N of Garage Door)
            new WallSegment { startPoint = new Vector3(foyerWidth, 0, 0), endPoint = new Vector3(foyerWidth, 0, 0.5f), isExterior = false }, // E (S of LR Opening)
            new WallSegment { startPoint = new Vector3(foyerWidth, 0, 5.5f), endPoint = new Vector3(foyerWidth, 0, foyerDepth), isExterior = false }, // E (N of LR Opening)
            new WallSegment { startPoint = new Vector3(0, 0, foyerDepth), endPoint = new Vector3(1f, 0, foyerDepth), isExterior = false }, // N (W of Hall Opening)
            new WallSegment { startPoint = new Vector3(5f, 0, foyerDepth), endPoint = new Vector3(foyerWidth, 0, foyerDepth), isExterior = false }  // N (E of Hall Opening)
        },
        connectedRoomIds = new List<string> { "CoveredEntry", "LivingRoom", "Garage", "CentralHallway" },
        notes = "North of Covered Entry, East of Garage."
    });

    plan.rooms.Add(new RoomData { // Office
        roomId = "Office", roomLabel = "Office", dimensions = new Vector2(officeWidth, officeDepth),
        position = officePos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, officeDepth), endPoint = new Vector3(officeWidth, 0, officeDepth), isExterior = false }, // N (to HallBath)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0, 0, 3.5f), isExterior = true }, // W (S of Window)
            new WallSegment { startPoint = new Vector3(0, 0, 6.5f), endPoint = new Vector3(0, 0, officeDepth), isExterior = true }, // W (N of Window)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(officeWidth, 0, 0), isExterior = false }, // S (to Garage)
            new WallSegment { startPoint = new Vector3(officeWidth, 0, 8.667f), endPoint = new Vector3(officeWidth, 0, officeDepth), isExterior = false } // E (N of Closet opening to Hall)
        },
        connectedRoomIds = new List<string> { "CentralHallway", "Garage", "HallBath" },
        notes = "North of Garage, West of Central Hall."
    });

    plan.rooms.Add(new RoomData { // HallBath
        roomId = "HallBath", roomLabel = "Hall Bath", dimensions = new Vector2(hallBathWidth, hallBathDepth),
        position = hallBathPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, hallBathDepth), endPoint = new Vector3(hallBathWidth, 0, hallBathDepth), isExterior = false }, // N (to Master Suite)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0, 0, hallBathDepth), isExterior = true }, // W (Exterior)
            new WallSegment { startPoint = new Vector3(hallBathWidth, 0, 2.667f), endPoint = new Vector3(hallBathWidth, 0, hallBathDepth), isExterior = false }, // E (N of Door to Hall)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(hallBathWidth, 0, 0), isExterior = false } // S (to Office)
        },
        connectedRoomIds = new List<string> { "CentralHallway", "Office", "MasterBedroom" },
        notes = "North of Office."
    });

    plan.rooms.Add(new RoomData { // MasterBedroom
        roomId = "MasterBedroom", roomLabel = "Master Bedroom", dimensions = new Vector2(mbWidth, mbDepth),
        position = mbPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, mbDepth), endPoint = new Vector3(1.667f, 0, mbDepth), isExterior = true }, // N (W of Win1)
            new WallSegment { startPoint = new Vector3(4.667f, 0, mbDepth), endPoint = new Vector3(6.334f, 0, mbDepth), isExterior = true }, // N (Between Wins)
            new WallSegment { startPoint = new Vector3(9.334f, 0, mbDepth), endPoint = new Vector3(mbWidth, 0, mbDepth), isExterior = true }, // N (E of Win2)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0, 0, mbDepth), isExterior = true }, // W (Exterior)
            new WallSegment { startPoint = new Vector3(mbWidth, 0, 0), endPoint = new Vector3(mbWidth, 0, 3f), isExterior = false }, // E (S of Closet opening)
            new WallSegment { startPoint = new Vector3(mbWidth, 0, 9f), endPoint = new Vector3(mbWidth, 0, mbDepth), isExterior = false }, // E (N of Closet opening)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(mbWidth - 2.667f, 0, 0), isExterior = false } // S (W of Vestibule Door)
            // S (E of Vestibule Door) segment: new WallSegment { startPoint = new Vector3(mbWidth, 0, 0), endPoint = new Vector3(mbWidth, 0, 0), isExterior = false } - this is a point, door is at end. Assume door is 2.667 wide at the East end of south wall. So X=mbWidth-2.667 to mbWidth.
        },
        connectedRoomIds = new List<string> { "MasterVestibule", "HallBath" },
        notes = "NW corner of house."
    });

    plan.rooms.Add(new RoomData { // MasterCloset
        roomId = "MasterCloset", roomLabel = "Master Closet", dimensions = new Vector2(mcWidth, mcDepth),
        position = mcPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, mcDepth), endPoint = new Vector3(mcWidth, 0, mcDepth), isExterior = false }, // N
            new WallSegment { startPoint = new Vector3(mcWidth, 0, 0), endPoint = new Vector3(mcWidth, 0, mcDepth), isExterior = false }, // E (Could be ext if MB is on corner)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(mcWidth, 0, 0), isExterior = false }  // S
            // West wall open to MasterBedroom
        },
        connectedRoomIds = new List<string> { "MasterBedroom" }, notes = "Shallow closet room."
    });

    plan.rooms.Add(new RoomData { // MasterVestibule
        roomId = "MasterVestibule", roomLabel = "Master Vestibule", dimensions = new Vector2(mvWidth, mvDepth),
        position = mvPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0, 0, mvDepth), endPoint = new Vector3(0.6665f, 0, mvDepth), isExterior = false }, // N (W of MB Door)
            new WallSegment { startPoint = new Vector3(3.3335f, 0, mvDepth), endPoint = new Vector3(mvWidth, 0, mvDepth), isExterior = false }, // N (E of MB Door)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0, 0, mvDepth), isExterior = false }, // W
            new WallSegment { startPoint = new Vector3(mvWidth, 0, 0), endPoint = new Vector3(mvWidth, 0, 0.5f), isExterior = false }, // E (S of Display)
            new WallSegment { startPoint = new Vector3(mvWidth, 0, 2.5f), endPoint = new Vector3(mvWidth, 0, mvDepth), isExterior = false }, // E (N of Display)
            new WallSegment { startPoint = new Vector3(0, 0, 0), endPoint = new Vector3(0.6665f, 0, 0), isExterior = false }, // S (W of MBath Door)
            new WallSegment { startPoint = new Vector3(3.3335f, 0, 0), endPoint = new Vector3(mvWidth, 0, 0), isExterior = false }  // S (E of MBath Door)
        },
        connectedRoomIds = new List<string> { "MasterBedroom", "MasterBath", "FamilyRoom" },
        notes = "Transition to Master Bath."
    });

    plan.rooms.Add(new RoomData { // MasterBath
        roomId = "MasterBath", roomLabel = "Master Bath", dimensions = new Vector2(masterBathWidth, masterBathDepth),
        position = masterBathPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0,0,masterBathDepth), endPoint = new Vector3(1.6665f,0,masterBathDepth), isExterior = false }, // N (W of Vest Door)
            new WallSegment { startPoint = new Vector3(4.3335f,0,masterBathDepth), endPoint = new Vector3(masterBathWidth,0,masterBathDepth), isExterior = false }, // N (E of Vest Door)
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,3.25f), isExterior = true }, // W (S of Window)
            new WallSegment { startPoint = new Vector3(0,0,5.25f), endPoint = new Vector3(0,0,masterBathDepth), isExterior = true }, // W (N of Window)
            new WallSegment { startPoint = new Vector3(masterBathWidth,0,0), endPoint = new Vector3(masterBathWidth,0,masterBathDepth), isExterior = false }, // E (Interior)
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(masterBathWidth,0,0), isExterior = true } // S (Exterior)
        },
        connectedRoomIds = new List<string> { "MasterVestibule" },
        notes = "South of Master Vestibule."
    });

    plan.rooms.Add(new RoomData { // LivingRoom
        roomId = "LivingRoom", roomLabel = "Living Room", dimensions = new Vector2(lrWidth, lrDepth),
        position = lrPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(2.3335f,0,0), isExterior = true }, // S (W of Bay)
            new WallSegment { startPoint = new Vector3(10.3335f,0,0), endPoint = new Vector3(lrWidth,0,0), isExterior = true }, // S (E of Bay)
            // Bay window segments (3 parts, Z=-2 for front face. X for side faces go from Z=0 to Z=-2)
            new WallSegment { startPoint = new Vector3(2.3335f, 0, 0), endPoint = new Vector3(2.3335f - 1f, 0, -2f), isExterior = true }, // Angled West Bay Side (approx)
            new WallSegment { startPoint = new Vector3(2.3335f - 1f, 0, -2f), endPoint = new Vector3(10.3335f + 1f, 0, -2f), isExterior = true }, // Bay Front
            new WallSegment { startPoint = new Vector3(10.3335f + 1f, 0, -2f), endPoint = new Vector3(10.3335f, 0, 0), isExterior = true }, // Angled East Bay Side (approx)
            new WallSegment { startPoint = new Vector3(0,0,5.5f), endPoint = new Vector3(0,0,lrDepth), isExterior = false }, // W (N of Foyer Opening)
            // West wall Z=0 to Z=5.5 for foyer opening was missed. Assuming foyer opening is 5ft deep along this 15ft wall.
            // Foyer only 6ft deep. Cased opening from Foyer to LR is on Foyer's East wall.
            // Let's assume Foyer cased opening is Z=0.5 to Z=5.5 on LR West wall.
            // new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,0.5f), isExterior = false }, // W (S of Foyer Opening) - REPLACED by prior entry

            new WallSegment { startPoint = new Vector3(lrWidth,0,0), endPoint = new Vector3(lrWidth,0,5.5f), isExterior = true }, // E (S of Fireplace)
            new WallSegment { startPoint = new Vector3(lrWidth,0,9.5f), endPoint = new Vector3(lrWidth,0,lrDepth), isExterior = true }, // E (N of Fireplace)
            new WallSegment { startPoint = new Vector3(0,0,lrDepth), endPoint = new Vector3(4.8335f,0,lrDepth), isExterior = false }, // N (W of DR Opening)
            new WallSegment { startPoint = new Vector3(7.8335f,0,lrDepth), endPoint = new Vector3(lrWidth,0,lrDepth), isExterior = false }  // N (E of DR Opening)
        },
        connectedRoomIds = new List<string> { "Foyer", "DiningRoom" }, notes = "SE Corner."
    });

    plan.rooms.Add(new RoomData { // DiningRoom
        roomId = "DiningRoom", roomLabel = "Dining Room", dimensions = new Vector2(drWidth, drDepth),
        position = drPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(3.1665f,0,0), isExterior = false }, // S (W of LR Opening)
            new WallSegment { startPoint = new Vector3(6.1665f,0,0), endPoint = new Vector3(drWidth,0,0), isExterior = false }, // S (E of LR Opening)
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,1f), isExterior = false }, // W (S of Stair Railing)
            new WallSegment { startPoint = new Vector3(0,0,drDepth-1f), endPoint = new Vector3(0,0,drDepth), isExterior = false }, // W (N of Stair Railing)
            new WallSegment { startPoint = new Vector3(drWidth,0,0), endPoint = new Vector3(drWidth,0,1f), isExterior = true }, // E (S of China)
            new WallSegment { startPoint = new Vector3(drWidth,0,4f), endPoint = new Vector3(drWidth,0,5f), isExterior = true }, // E (Betw China/Win)
            new WallSegment { startPoint = new Vector3(drWidth,0,8f), endPoint = new Vector3(drWidth,0,drDepth), isExterior = true }, // E (N of Win)
            new WallSegment { startPoint = new Vector3(0,0,drDepth), endPoint = new Vector3(2.6665f,0,drDepth), isExterior = false }, // N (W of K Passthru)
            new WallSegment { startPoint = new Vector3(6.6665f,0,drDepth), endPoint = new Vector3(drWidth,0,drDepth), isExterior = false }  // N (E of K Passthru)
        },
        connectedRoomIds = new List<string> { "LivingRoom", "Kitchen", "Staircase" },
        notes = "North of Living Room."
    });

    plan.rooms.Add(new RoomData { // Kitchen
        roomId = "Kitchen", roomLabel = "Kitchen", dimensions = new Vector2(kWidth, kDepth),
        position = kPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0,0,kDepth), endPoint = new Vector3(3.5f,0,kDepth), isExterior = false }, // N (W of Win to Nook)
            new WallSegment { startPoint = new Vector3(6.5f,0,kDepth), endPoint = new Vector3(kWidth,0,kDepth), isExterior = false }, // N (E of Win to Nook)
            new WallSegment { startPoint = new Vector3(0,0,kDepth-1f), endPoint = new Vector3(0,0,kDepth), isExterior = false }, // W (N part of Peninsula return)
            new WallSegment { startPoint = new Vector3(kWidth,0,2.667f), endPoint = new Vector3(kWidth,0,7f), isExterior = true }, // E (Betw CP Door & Win area)
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(3f,0,0), isExterior = false }, // S (W of DR Passthru)
            new WallSegment { startPoint = new Vector3(7f,0,0), endPoint = new Vector3(kWidth,0,0), isExterior = false }  // S (E of DR Passthru)
        },
        connectedRoomIds = new List<string> { "DiningRoom", "Nook", "FamilyRoom", "CoveredPatio" },
        notes = "North of Dining Room."
    });

    plan.rooms.Add(new RoomData { // Nook
        roomId = "Nook", roomLabel = "Nook", dimensions = new Vector2(nookWidth, nookDepth),
        position = nookPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0,0,nookDepth), endPoint = new Vector3(0.5f,0,nookDepth), isExterior = true }, // N (W of Slider)
            new WallSegment { startPoint = new Vector3(5.5f,0,nookDepth), endPoint = new Vector3(nookWidth,0,nookDepth), isExterior = true }, // N (E of Slider)
            new WallSegment { startPoint = new Vector3(nookWidth,0,2.667f), endPoint = new Vector3(nookWidth,0,nookDepth), isExterior = true }, // E (N of CP Door)
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3( (nookWidth-3f)/2f,0,0), isExterior = false }, // S (W of K Window)
            new WallSegment { startPoint = new Vector3( (nookWidth-3f)/2f + 3f ,0,0), endPoint = new Vector3(nookWidth,0,0), isExterior = false }  // S (E of K Window)
            // West wall open to Family Room
        },
        connectedRoomIds = new List<string> { "Kitchen", "FamilyRoom", "CoveredPatio" },
        notes = "North of Kitchen."
    });

    plan.rooms.Add(new RoomData { // FamilyRoom
        roomId = "FamilyRoom", roomLabel = "Family Room", dimensions = new Vector2(frWidth, frDepth),
        position = frPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0,0,frDepth), endPoint = new Vector3(2.111f,0,frDepth), isExterior = true }, // N (W of Win1)
            new WallSegment { startPoint = new Vector3(5.111f,0,frDepth), endPoint = new Vector3(7.222f,0,frDepth), isExterior = true }, // N (Betw Wins)
            new WallSegment { startPoint = new Vector3(10.222f,0,frDepth), endPoint = new Vector3(frWidth,0,frDepth), isExterior = true }, // N (E of Win2)
            new WallSegment { startPoint = new Vector3(0,0,3f), endPoint = new Vector3(0,0,frDepth), isExterior = false }, // W (N of MV Opening)
            new WallSegment { startPoint = new Vector3(frWidth,0,0), endPoint = new Vector3(frWidth,0, frDepth-2.5f), isExterior = true }, // E (S of CP Door, this wall is ext.)
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(frWidth-4f,0,0), isExterior = false } // S (W of Hall Opening)
            // East wall open to Kitchen/Nook except for the door to CP.
        },
        connectedRoomIds = new List<string> { "Nook", "Kitchen", "CentralHallway", "MasterVestibule", "CoveredPatio" },
        notes = "North-Central."
    });

    plan.rooms.Add(new RoomData { // CoveredPatio
        roomId = "CoveredPatio", roomLabel = "Covered Patio", dimensions = new Vector2(cpWidth, cpDepth),
        position = cpPos,
        walls = new List<WallSegment> {
            new WallSegment { startPoint = new Vector3(0,0,cpDepth), endPoint = new Vector3(cpWidth,0,cpDepth), isExterior = true }, // N
            new WallSegment { startPoint = new Vector3(cpWidth,0,0), endPoint = new Vector3(cpWidth,0,cpDepth), isExterior = true }, // E
            new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(cpWidth,0,0), isExterior = true }  // S
            // West side open to house doors
        },
        connectedRoomIds = new List<string> { "Kitchen", "Nook", "FamilyRoom" },
        notes = "NE area, attached to K,N,FR."
    });

    // CentralHallway: Its walls are largely defined by the rooms around it.
    // For now, its own 'walls' list is empty. If specific segments are needed (e.g. for a closet door in the hall not part of another room), add them.
    RoomData centralHallwayRoom = new RoomData {
        roomId = "CentralHallway", roomLabel = "Central Hallway", dimensions = new Vector2(hallWidth, hallDepthEstimate),
        position = hallPos,
        walls = new List<WallSegment>(), // TODO: Could define segments for closet door, etc.
        connectedRoomIds = new List<string> { "Foyer", "Office", "HallBath", "MasterVestibule", "FamilyRoom", "StairwellEnclosure" },
        notes = "Central circulation spine."
    };
    // Example: Closet door on West wall of Hall. Hall X=[11.333, 14.333]. Closet door on this wall.
    // Assume closet door (2ft wide) is Z= (foyerDepth + stairDepthEstimate - closetDepth) to (foyerDepth + stairDepthEstimate)
    // This is highly dependent on exact stair and closet placement.
    // For simplicity, I'll add placeholder segments for its typical boundaries.
    // West wall of Hall (borders Office, HallBath, MasterVestibule) - these rooms will define their East walls.
    // East wall of Hall (borders Staircase, DiningRoom) - these rooms will define their West walls.
    plan.rooms.Add(centralHallwayRoom);


    // Staircase: Will be handled by RoomBuilder's "StairwellEnclosure" logic to build 4 walls.
    // If one side is an open railing, RoomBuilder needs modification, or Staircase needs 3 WallSegments here.
    RoomData staircaseRoomData = new RoomData { // Re-add definition if it was modified earlier or ensure it's set correctly
        roomId = "Staircase", roomLabel = "Staircase", // Will be renamed to StairwellEnclosure
        dimensions = new Vector2(stairWidth, stairDepthEstimate),
        position = stairPos,
        walls = new List<WallSegment>(), // Empty, RoomBuilder makes the enclosure.
        connectedRoomIds = new List<string> { "Foyer", "CentralHallway", "DiningRoom" },
        notes = "Central staircase."
    };
    plan.rooms.Add(staircaseRoomData);

    int staircaseIndex = plan.rooms.FindIndex(r => r.roomId == "Staircase");
    if (staircaseIndex != -1) {
        RoomData tempStair = plan.rooms[staircaseIndex];
        tempStair.roomId = "StairwellEnclosure"; // For RoomBuilder logic
        tempStair.roomLabel = "Stairwell Enclosure";
        plan.rooms[staircaseIndex] = tempStair;
    }

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
