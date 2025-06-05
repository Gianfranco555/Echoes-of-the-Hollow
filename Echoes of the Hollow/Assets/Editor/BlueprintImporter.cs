using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

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
        float garageWidth = 11f + 4f/12f; // 11.333333f
        float garageDepth = 20f;
        float coveredEntryWidth = 6f;
        float coveredEntryDepth = 3f;
        float foyerWidth = 6f;
        float foyerDepth = 6f;
        float lrWidth = 12f + 8f/12f; // 12.666667f
        float lrDepth = 15f;
        float officeWidth = 9f + 2f/12f; // 9.166667f
        float officeDepth = 10f;
        float hallWidth = 3f;
        float hallDepthEstimate = 22f; // Approximate, adjust based on connected rooms
        float stairWidth = 3.5f;
        float stairDepthEstimate = 12f;
        float hallBathWidth = 5f;
        float hallBathDepth = 9f;
        float mbWidth = 11f;
        float mbDepth = 12f;
        float mcWidth = 2f; // Master Closet depth from wall
        float mcDepth = 6f; // Master Closet length along wall
        float mvWidth = 4f;
        float mvDepth = 3f;
        float masterBathWidth = 6f;
        float masterBathDepth = 8.5f;
        float drWidth = 9f + 4f/12f; // 9.333333f
        float drDepth = 10f + 4f/12f; // 10.333333f
        float kWidth = 10f;
        float kDepth = 9f;
        float nookWidth = 6f;
        float nookDepth = 7f;
        float frWidth = 12f + 4f/12f; // 12.333333f
        float frDepth = 15f + 6f/12f; // 15.5f
        float cpWidth = 6f; // Covered Patio depth from house (E-W)
        float cpDepth = 6f; // Covered Patio length along house (N-S)

        // --- Calculate Global Positions (SW corner of each room) ---
        Vector3 garagePos = Vector3.zero;                                                                          // SW Origin
        Vector3 foyerPos = new Vector3(garagePos.x + garageWidth, garagePos.z, 0);                                 // E of Garage
        Vector3 coveredEntryPos = new Vector3(foyerPos.x + (foyerWidth - coveredEntryWidth) / 2f, 0, foyerPos.z - coveredEntryDepth); // S of Foyer
        Vector3 lrPos = new Vector3(foyerPos.x + foyerWidth, foyerPos.z, 0);                                       // E of Foyer
        float houseEastmostX = lrPos.x + lrWidth;

        Vector3 officePos = new Vector3(garagePos.x, 0, garagePos.z + garageDepth);                                // N of Garage
        Vector3 hallBathPos = new Vector3(officePos.x, 0, officePos.z + officeDepth);                              // N of Office
        Vector3 mbPos = new Vector3(hallBathPos.x, 0, hallBathPos.z + hallBathDepth);                              // N of HallBath (NW corner of house)
        float houseNorthmostZ_WestWing = mbPos.z + mbDepth;

        // Central Hallway: Starts North of Foyer. Its West edge aligns with Foyer's West edge.
        Vector3 hallPos = new Vector3(foyerPos.x, 0, foyerPos.z + foyerDepth);
        // Staircase: East of Hallway's initial segment, North of Foyer.
        Vector3 stairPos = new Vector3(hallPos.x + hallWidth, 0, hallPos.z);

        // Master Vestibule: South of Master Bedroom's south wall, towards its East side.
        Vector3 mvPos = new Vector3(mbPos.x + mbWidth - mvWidth, 0, mbPos.z - mvDepth);

        // Master Bath: South of Vestibule.
        Vector3 masterBathPos = new Vector3(mvPos.x + mvWidth - masterBathWidth, 0, mvPos.z - masterBathDepth);

        // Master Closet: On Master Bedroom's East wall (X = mbPos.x + mbWidth).
        Vector3 mcPos = new Vector3(mbPos.x + mbWidth, 0, mbPos.z + (mbDepth - mcDepth) / 2f);

        // Dining Room: North of Living Room's North edge (lrPos.z + lrDepth). East of Staircase's East edge.
        Vector3 drPos = new Vector3(stairPos.x + stairWidth, 0, lrPos.z + lrDepth);

        // Kitchen: North of Dining Room. Let its East edge align with houseEastmostX.
        Vector3 kPos = new Vector3(houseEastmostX - kWidth, 0, drPos.z + drDepth);

        // Nook: North of Kitchen. Let its East edge align with houseEastmostX.
        Vector3 nookPos = new Vector3(houseEastmostX - nookWidth, 0, kPos.z + kDepth);
        float houseNorthmostZ_EastWing = nookPos.z + nookDepth;

        // Family Room: West of Kitchen/Nook. Its East edge aligns with Kitchen's West edge (kPos.x).
        Vector3 frPos = new Vector3(kPos.x - frWidth, 0, houseNorthmostZ_EastWing - frDepth);

        // Covered Patio: East of house's main East facade (houseEastmostX). Aligns with Nook's Z position.
        Vector3 cpPos = new Vector3(houseEastmostX, 0, nookPos.z);

        // --- Room Definitions with Global Positions & Local Wall Segments ---
        plan.rooms.Add(new RoomData {
            roomId = "OverallFootprint", roomLabel = "Overall Footprint",
            dimensions = new Vector2(houseEastmostX, Mathf.Max(houseNorthmostZ_WestWing, houseNorthmostZ_EastWing)),
            position = Vector3.zero, walls = new List<WallSegment>(), connectedRoomIds = new List<string>(),
            notes = "Overall house footprint."
        });

        plan.rooms.Add(new RoomData { /* Garage */
            roomId = "Garage", roomLabel = "Garage", dimensions = new Vector2(garageWidth, garageDepth),
            position = garagePos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,garageDepth), endPoint = new Vector3(garageWidth,0,garageDepth), isExterior = false }, // N (to Office)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,6f), isExterior = true }, // W (S of OHD)
                new WallSegment { startPoint = new Vector3(0,0,14f), endPoint = new Vector3(0,0,garageDepth), isExterior = true }, // W (N of OHD)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(1f,0,0), isExterior = true }, // S (W of Ped Door)
                new WallSegment { startPoint = new Vector3(3.667f,0,0), endPoint = new Vector3(garageWidth,0,0), isExterior = true }, // S (E of Ped Door)
                new WallSegment { startPoint = new Vector3(garageWidth,0,0), endPoint = new Vector3(garageWidth,0,8.6665f), isExterior = false }, // E (S of Foyer Door)
                new WallSegment { startPoint = new Vector3(garageWidth,0,11.3335f), endPoint = new Vector3(garageWidth,0,garageDepth), isExterior = false } // E (N of Foyer Door)
            },
            connectedRoomIds = new List<string> { "Foyer", "Office" }, notes = "South-West corner of the house."
        });

        plan.rooms.Add(new RoomData { /* Covered Entry */
            roomId = "CoveredEntry", roomLabel = "Covered Entry", dimensions = new Vector2(coveredEntryWidth, coveredEntryDepth),
            position = coveredEntryPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,coveredEntryDepth), endPoint = new Vector3(coveredEntryWidth,0,coveredEntryDepth), isExterior = true },
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,coveredEntryDepth), isExterior = true },
                new WallSegment { startPoint = new Vector3(coveredEntryWidth,0,0), endPoint = new Vector3(coveredEntryWidth,0,coveredEntryDepth), isExterior = true }
            },
            connectedRoomIds = new List<string> { "Foyer" }, notes = "Exterior, South of Foyer."
        });

        plan.rooms.Add(new RoomData { /* Foyer */
            roomId = "Foyer", roomLabel = "Foyer", dimensions = new Vector2(foyerWidth, foyerDepth),
            position = foyerPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(1.5f,0,0), isExterior = true }, // S (W of Front Door)
                new WallSegment { startPoint = new Vector3(4.5f,0,0), endPoint = new Vector3(foyerWidth,0,0), isExterior = true }, // S (E of Front Door)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,1.6665f), isExterior = false }, // W (S of Garage Door)
                new WallSegment { startPoint = new Vector3(0,0,4.3335f), endPoint = new Vector3(0,0,foyerDepth), isExterior = false }, // W (N of Garage Door)
                new WallSegment { startPoint = new Vector3(foyerWidth,0,0), endPoint = new Vector3(foyerWidth,0,0.5f), isExterior = false }, // E (S of LR Opening)
                new WallSegment { startPoint = new Vector3(foyerWidth,0,5.5f), endPoint = new Vector3(foyerWidth,0,foyerDepth), isExterior = false }, // E (N of LR Opening)
                new WallSegment { startPoint = new Vector3(0,0,foyerDepth), endPoint = new Vector3(1f,0,foyerDepth), isExterior = false }, // N (W of Hall Opening)
                new WallSegment { startPoint = new Vector3(foyerWidth-1f,0,foyerDepth), endPoint = new Vector3(foyerWidth,0,foyerDepth), isExterior = false }  // N (E of Hall Opening, assuming hall opening is 4ft centered)
            },
            connectedRoomIds = new List<string> { "CoveredEntry", "LivingRoom", "Garage", "CentralHallway" },
            notes = "North of Covered Entry, East of Garage."
        });

        plan.rooms.Add(new RoomData { /* Office */
            roomId = "Office", roomLabel = "Office", dimensions = new Vector2(officeWidth, officeDepth),
            position = officePos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,officeDepth), endPoint = new Vector3(officeWidth,0,officeDepth), isExterior = false }, // N (to HallBath)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,3.5f), isExterior = true }, // W (S of Window)
                new WallSegment { startPoint = new Vector3(0,0,6.5f), endPoint = new Vector3(0,0,officeDepth), isExterior = true }, // W (N of Window)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(officeWidth,0,0), isExterior = false }, // S (to Garage)
                new WallSegment { startPoint = new Vector3(officeWidth,0,8.667f), endPoint = new Vector3(officeWidth,0,officeDepth), isExterior = false } // E (N of Closet/Door opening to Hall)
            },
            connectedRoomIds = new List<string> { "CentralHallway", "Garage", "HallBath" },
            notes = "North of Garage, West of Central Hall."
        });

        plan.rooms.Add(new RoomData { /* HallBath */
            roomId = "HallBath", roomLabel = "Hall Bath", dimensions = new Vector2(hallBathWidth, hallBathDepth),
            position = hallBathPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,hallBathDepth), endPoint = new Vector3(hallBathWidth,0,hallBathDepth), isExterior = false }, // N (to Master Suite)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,hallBathDepth), isExterior = true }, // W (Exterior)
                new WallSegment { startPoint = new Vector3(hallBathWidth,0,2.667f), endPoint = new Vector3(hallBathWidth,0,hallBathDepth), isExterior = false }, // E (N of Door to Hall)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(hallBathWidth,0,0), isExterior = false } // S (to Office)
            },
            connectedRoomIds = new List<string> { "CentralHallway", "Office", "MasterBedroom" },
            notes = "North of Office."
        });

        plan.rooms.Add(new RoomData { /* MasterBedroom */
            roomId = "MasterBedroom", roomLabel = "Master Bedroom", dimensions = new Vector2(mbWidth, mbDepth),
            position = mbPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,mbDepth), endPoint = new Vector3(1.667f,0,mbDepth), isExterior = true },
                new WallSegment { startPoint = new Vector3(4.667f,0,mbDepth), endPoint = new Vector3(6.334f,0,mbDepth), isExterior = true },
                new WallSegment { startPoint = new Vector3(9.334f,0,mbDepth), endPoint = new Vector3(mbWidth,0,mbDepth), isExterior = true },
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,mbDepth), isExterior = true }, // W
                new WallSegment { startPoint = new Vector3(mbWidth,0,0), endPoint = new Vector3(mbWidth,0,3f), isExterior = false }, // E (S of Closet)
                new WallSegment { startPoint = new Vector3(mbWidth,0,9f), endPoint = new Vector3(mbWidth,0,mbDepth), isExterior = false }, // E (N of Closet)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(mbWidth - 2.667f,0,0), isExterior = false } // S (W of Vestibule Door)
            },
            connectedRoomIds = new List<string> { "MasterVestibule", "HallBath" }, notes = "NW corner of house."
        });

        plan.rooms.Add(new RoomData { /* MasterCloset */
            roomId = "MasterCloset", roomLabel = "Master Closet", dimensions = new Vector2(mcWidth, mcDepth),
            position = mcPos, atticHatchLocalPosition = new Vector3(mcWidth/2f, plan.storyHeight, mcDepth/2f), 
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,mcDepth), endPoint = new Vector3(mcWidth,0,mcDepth), isExterior = false }, // N
                new WallSegment { startPoint = new Vector3(mcWidth,0,0), endPoint = new Vector3(mcWidth,0,mcDepth), isExterior = false }, // E
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(mcWidth,0,0), isExterior = false }  // S
            },
            connectedRoomIds = new List<string> { "MasterBedroom" }, notes = "Shallow closet room, assumed open on its West side to Master Bedroom."
        });

        plan.rooms.Add(new RoomData { /* MasterVestibule */
            roomId = "MasterVestibule", roomLabel = "Master Vestibule", dimensions = new Vector2(mvWidth, mvDepth),
            position = mvPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,mvDepth), endPoint = new Vector3(0.6665f,0,mvDepth), isExterior = false }, // N (W of MB Door)
                new WallSegment { startPoint = new Vector3(3.3335f,0,mvDepth), endPoint = new Vector3(mvWidth,0,mvDepth), isExterior = false }, // N (E of MB Door)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,mvDepth), isExterior = false }, // W
                new WallSegment { startPoint = new Vector3(mvWidth,0,0), endPoint = new Vector3(mvWidth,0,0.5f), isExterior = false }, // E (S of Display)
                new WallSegment { startPoint = new Vector3(mvWidth,0,2.5f), endPoint = new Vector3(mvWidth,0,mvDepth), isExterior = false }, // E (N of Display)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0.6665f,0,0), isExterior = false }, // S (W of MBath Door)
                new WallSegment { startPoint = new Vector3(3.3335f,0,0), endPoint = new Vector3(mvWidth,0,0), isExterior = false }  // S (E of MBath Door)
            },
            connectedRoomIds = new List<string> { "MasterBedroom", "MasterBath", "FamilyRoom", "CentralHallway" },
            notes = "Transition to Master Bath."
        });

        plan.rooms.Add(new RoomData { /* MasterBath */
            roomId = "MasterBath", roomLabel = "Master Bath", dimensions = new Vector2(masterBathWidth, masterBathDepth),
            position = masterBathPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,masterBathDepth), endPoint = new Vector3(1.6665f,0,masterBathDepth), isExterior = false }, // N (W of Vest Door)
                new WallSegment { startPoint = new Vector3(4.3335f,0,masterBathDepth), endPoint = new Vector3(masterBathWidth,0,masterBathDepth), isExterior = false }, // N (E of Vest Door)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,3.25f), isExterior = true }, // W (S of Window)
                new WallSegment { startPoint = new Vector3(0,0,5.25f), endPoint = new Vector3(0,0,masterBathDepth), isExterior = true }, // W (N of Window)
                new WallSegment { startPoint = new Vector3(masterBathWidth,0,0), endPoint = new Vector3(masterBathWidth,0,masterBathDepth), isExterior = false }, // E (Interior to Hall or other)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(masterBathWidth,0,0), isExterior = true } // S (Exterior)
            },
            connectedRoomIds = new List<string> { "MasterVestibule" },
            notes = "South of Master Vestibule."
        });

        plan.rooms.Add(new RoomData { /* LivingRoom */
            roomId = "LivingRoom", roomLabel = "Living Room", dimensions = new Vector2(lrWidth, lrDepth),
            position = lrPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(2.3335f,0,0), isExterior = true }, // S (W of Bay)
                new WallSegment { startPoint = new Vector3(10.3335f,0,0), endPoint = new Vector3(lrWidth,0,0), isExterior = true }, // S (E of Bay)
                new WallSegment { startPoint = new Vector3(2.3335f, 0, 0), endPoint = new Vector3(1.3335f, 0, -2f), isExterior = true }, // Angled W Bay Side
                new WallSegment { startPoint = new Vector3(1.3335f, 0, -2f), endPoint = new Vector3(11.3335f, 0, -2f), isExterior = true }, // Bay Front
                new WallSegment { startPoint = new Vector3(11.3335f, 0, -2f), endPoint = new Vector3(10.3335f, 0, 0), isExterior = true }, // Angled E Bay Side
                new WallSegment { startPoint = new Vector3(0,0,0.5f), endPoint = new Vector3(0,0,0.0f), isExterior = false }, // W (S of Foyer Opening - Adjusted to allow Foyer opening)
                new WallSegment { startPoint = new Vector3(0,0,5.5f), endPoint = new Vector3(0,0,lrDepth), isExterior = false }, // W (N of Foyer Opening)
                new WallSegment { startPoint = new Vector3(lrWidth,0,0), endPoint = new Vector3(lrWidth,0,5.5f), isExterior = true }, // E (S of Fireplace)
                new WallSegment { startPoint = new Vector3(lrWidth,0,9.5f), endPoint = new Vector3(lrWidth,0,lrDepth), isExterior = true }, // E (N of Fireplace)
                new WallSegment { startPoint = new Vector3(0,0,lrDepth), endPoint = new Vector3(4.8335f,0,lrDepth), isExterior = false }, // N (W of DR Opening)
                new WallSegment { startPoint = new Vector3(7.8335f,0,lrDepth), endPoint = new Vector3(lrWidth,0,lrDepth), isExterior = false }  // N (E of DR Opening)
            },
            connectedRoomIds = new List<string> { "Foyer", "DiningRoom" }, notes = "SE Corner."
        });

        plan.rooms.Add(new RoomData { /* DiningRoom */
            roomId = "DiningRoom", roomLabel = "Dining Room", dimensions = new Vector2(drWidth, drDepth),
            position = drPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(3.1665f,0,0), isExterior = false }, // S (W of LR Opening)
                new WallSegment { startPoint = new Vector3(6.1665f,0,0), endPoint = new Vector3(drWidth,0,0), isExterior = false }, // S (E of LR Opening)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(0,0,1f), isExterior = false }, // Short stub S
                new WallSegment { startPoint = new Vector3(0,0,drDepth-1f), endPoint = new Vector3(0,0,drDepth), isExterior = false }, // Short stub N
                new WallSegment { startPoint = new Vector3(drWidth,0,0), endPoint = new Vector3(drWidth,0,1f), isExterior = true }, // E (S of China)
                new WallSegment { startPoint = new Vector3(drWidth,0,4f), endPoint = new Vector3(drWidth,0,5f), isExterior = true }, // E (Betw China/Win)
                new WallSegment { startPoint = new Vector3(drWidth,0,8f), endPoint = new Vector3(drWidth,0,drDepth), isExterior = true }, // E (N of Win)
                new WallSegment { startPoint = new Vector3(0,0,drDepth), endPoint = new Vector3(2.6665f,0,drDepth), isExterior = false }, // N (W of K Passthru)
                new WallSegment { startPoint = new Vector3(6.6665f,0,drDepth), endPoint = new Vector3(drWidth,0,drDepth), isExterior = false }  // N (E of K Passthru)
            },
            connectedRoomIds = new List<string> { "LivingRoom", "Kitchen", "StairwellEnclosure" },
            notes = "North of Living Room."
        });

        plan.rooms.Add(new RoomData { /* Kitchen */
            roomId = "Kitchen", roomLabel = "Kitchen", dimensions = new Vector2(kWidth, kDepth),
            position = kPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,kDepth), endPoint = new Vector3(3.5f,0,kDepth), isExterior = false }, // N (W of Win to Nook)
                new WallSegment { startPoint = new Vector3(6.5f,0,kDepth), endPoint = new Vector3(kWidth,0,kDepth), isExterior = false }, // N (E of Win to Nook)
                new WallSegment { startPoint = new Vector3(0,0,kDepth- (kDepth - 3f) ), endPoint = new Vector3(0,0,kDepth), isExterior = false }, // W (N part of Peninsula return)
                
                // Kitchen East Wall (Exterior) - Modified to fill gaps
                new WallSegment { startPoint = new Vector3(kWidth,0,0), endPoint = new Vector3(kWidth,0,2.667f), isExterior = true }, // E (South part, filling gap)
                new WallSegment { startPoint = new Vector3(kWidth,0,2.667f), endPoint = new Vector3(kWidth,0,7f), isExterior = true }, // E (Original segment, Betw CP Door & Win area)
                new WallSegment { startPoint = new Vector3(kWidth,0,7f), endPoint = new Vector3(kWidth,0,kDepth), isExterior = true }, // E (North part, filling gap)
                
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(3f,0,0), isExterior = false }, // S (W of DR Passthru)
                new WallSegment { startPoint = new Vector3(7f,0,0), endPoint = new Vector3(kWidth,0,0), isExterior = false }  // S (E of DR Passthru)
            },
            connectedRoomIds = new List<string> { "DiningRoom", "Nook", "FamilyRoom", "CoveredPatio" },
            notes = "North of Dining Room."
        });

        plan.rooms.Add(new RoomData { /* Nook */
            roomId = "Nook", roomLabel = "Nook", dimensions = new Vector2(nookWidth, nookDepth),
            position = nookPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,nookDepth), endPoint = new Vector3(0.5f,0,nookDepth), isExterior = true }, // N (W of Slider)
                new WallSegment { startPoint = new Vector3(5.5f,0,nookDepth), endPoint = new Vector3(nookWidth,0,nookDepth), isExterior = true }, // N (E of Slider)
                new WallSegment { startPoint = new Vector3(nookWidth,0,2.667f), endPoint = new Vector3(nookWidth,0,nookDepth), isExterior = true }, // E (N of CP Door)
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3((nookWidth-3f)/2f,0,0), isExterior = false }, // S (W of K Window opening)
                new WallSegment { startPoint = new Vector3((nookWidth-3f)/2f + 3f,0,0), endPoint = new Vector3(nookWidth,0,0), isExterior = false }  // S (E of K Window opening)
            },
            connectedRoomIds = new List<string> { "Kitchen", "FamilyRoom", "CoveredPatio" },
            notes = "North of Kitchen."
        });

        plan.rooms.Add(new RoomData { /* FamilyRoom */
            roomId = "FamilyRoom", roomLabel = "Family Room", dimensions = new Vector2(frWidth, frDepth),
            position = frPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,frDepth), endPoint = new Vector3(2.111f,0,frDepth), isExterior = true }, // N (W of Win1)
                new WallSegment { startPoint = new Vector3(5.111f,0,frDepth), endPoint = new Vector3(7.222f,0,frDepth), isExterior = true }, // N (Betw Wins)
                new WallSegment { startPoint = new Vector3(10.222f,0,frDepth), endPoint = new Vector3(frWidth,0,frDepth), isExterior = true }, // N (E of Win2)
                new WallSegment { startPoint = new Vector3(0,0,3f), endPoint = new Vector3(0,0,frDepth), isExterior = false }, // W (N of MV Opening)
                new WallSegment { startPoint = new Vector3(frWidth,0,0), endPoint = new Vector3(frWidth,0, frDepth-2.5f), isExterior = false }, // S of CP Door (interior to Kitchen)
                new WallSegment { startPoint = new Vector3(3f,0,0), endPoint = new Vector3(frWidth-4f,0,0), isExterior = false } // S (Between MV and Hall openings)
            },
            connectedRoomIds = new List<string> { "Nook", "Kitchen", "CentralHallway", "MasterVestibule", "CoveredPatio" },
            notes = "North-Central."
        });

        plan.rooms.Add(new RoomData { /* CoveredPatio */
            roomId = "CoveredPatio", roomLabel = "Covered Patio", dimensions = new Vector2(cpWidth, cpDepth),
            position = cpPos,
            walls = new List<WallSegment> {
                new WallSegment { startPoint = new Vector3(0,0,cpDepth), endPoint = new Vector3(cpWidth,0,cpDepth), isExterior = true }, // N
                new WallSegment { startPoint = new Vector3(cpWidth,0,0), endPoint = new Vector3(cpWidth,0,cpDepth), isExterior = true }, // E
                new WallSegment { startPoint = new Vector3(0,0,0), endPoint = new Vector3(cpWidth,0,0), isExterior = true }  // S
            },
            connectedRoomIds = new List<string> { "Kitchen", "Nook", "FamilyRoom" },
            notes = "NE area, attached to K,N,FR."
        });

        RoomData centralHallwayRoom = new RoomData {
            roomId = "CentralHallway", roomLabel = "Central Hallway", dimensions = new Vector2(hallWidth, hallDepthEstimate),
            position = hallPos,
            walls = new List<WallSegment>(), 
            connectedRoomIds = new List<string> { "Foyer", "Office", "HallBath", "MasterVestibule", "FamilyRoom", "StairwellEnclosure" },
            notes = "Central circulation spine."
        };
        plan.rooms.Add(centralHallwayRoom);

        RoomData staircaseRoomData = new RoomData {
            roomId = "Staircase", roomLabel = "Staircase",
            dimensions = new Vector2(stairWidth, stairDepthEstimate),
            position = stairPos,
            walls = new List<WallSegment>(), 
            connectedRoomIds = new List<string> { "Foyer", "CentralHallway", "DiningRoom" },
            notes = "Central staircase."
        };
        plan.rooms.Add(staircaseRoomData);

        int staircaseIndex = plan.rooms.FindIndex(r => r.roomId == "Staircase");
        if (staircaseIndex != -1) {
            RoomData tempStair = plan.rooms[staircaseIndex];
            tempStair.roomId = "StairwellEnclosure";
            tempStair.roomLabel = "Stairwell Enclosure";
            plan.rooms[staircaseIndex] = tempStair;
        }
        
        plan.windows.Add(new WindowSpec
        {
            windowId = "LivingRoom_SouthTestWindow",
            type = WindowType.SingleHung, 
            width = 3f,
            height = 4f,
            position = new Vector3(1f, 0f, 0f), 
            sillHeight = 3f, 
            wallId = "Wall_LivingRoom_7", 
            isOperable = true
        });

        plan.doors.Add(new DoorSpec { doorId = "ExampleFoyerToGarage", type = DoorType.Hinged, width = 2.667f, height = 6.667f, wallId = "Wall_Foyer_W_Segment1", connectsRoomA_Id="Foyer", connectsRoomB_Id="Garage" });
        plan.openings.Add(new OpeningSpec { openingId = "ExampleFoyerToLiving", type = OpeningType.CasedOpening, width = 5f, height = 6.667f, wallId = "Wall_Foyer_E_Segment1" });
    }

    [MenuItem("House Tools/Create House Plan from Blueprint")]
    private static void CreateHousePlanMenuItem()
    {
        string defaultName = "NewHousePlan.asset";
        string directory = "Assets/BlueprintData";
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
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
