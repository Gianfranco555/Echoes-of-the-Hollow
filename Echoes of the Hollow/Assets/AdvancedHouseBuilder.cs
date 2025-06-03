using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // Required for [MenuItem]
#endif

public enum Swing { InLeft, InRight, OutLeft, OutRight, SlideLeft, SlideRight }

public class AdvancedHouseBuilder : MonoBehaviour
{
    public const float FT = 0.3048f;

    // General Dimensions
    static readonly float WALL_THICKNESS = 0.5f * FT;
    static readonly float DOOR_WIDTH = 3f * FT;
    static readonly float DOOR_HEIGHT = 7f * FT;
    static readonly float FLOOR_HEIGHT = 8f * FT; // Same as MAIN_FLOOR_WALL_HEIGHT
    public const float WIDE_CASED_OPENING_WIDTH = 5f * FT;

    // Kitchen Specific
    static readonly float COUNTER_HEIGHT = 3f * FT;
    static readonly float COUNTER_DEPTH = 2f * FT;
    static readonly float FRIDGE_WIDTH = 3f * FT;
    static readonly float FRIDGE_HEIGHT = 6f * FT;
    static readonly float FRIDGE_DEPTH = 2.5f * FT;

    // Stairs Specific
    static readonly float STAIR_WIDTH = 3f * FT;
    static readonly float STAIR_RISER_HEIGHT = 7f / 12f * FT; // 7" rise
    static readonly float STAIR_TREAD_DEPTH = 11f / 12f * FT; // 11" tread

    // Storey Heights & Thicknesses
    static readonly float MAIN_FLOOR_WALL_HEIGHT = FLOOR_HEIGHT;
    static readonly float FLOOR_THICKNESS = WALL_THICKNESS;
    static readonly float CEILING_THICKNESS = WALL_THICKNESS;
    static readonly float ATTIC_WALL_HEIGHT = 4f * FT;
    static readonly float ROOF_RISE = 4f * FT;

    // Room Specific Interior Dimensions (add more as rooms are defined)
    static readonly float W_GARAGE_INT = 11.33f * FT;
    static readonly float D_GARAGE_INT = 20f * FT;
    static readonly float W_FOYER_INT = 6f * FT;
    static readonly float D_FOYER_INT = 8f * FT;
    static readonly float W_LIVING_INT = 12.67f * FT;
    static readonly float D_LIVING_INT = 15f * FT;
    static readonly float W_DINING_INT = 9.33f * FT;
    static readonly float D_DINING_INT = 10.33f * FT;
    static readonly float W_KITCHEN_INT = 12f * FT;
    static readonly float D_KITCHEN_INT = 12f * FT;
    static readonly float W_NOOK_INT = 8f * FT;
    static readonly float D_NOOK_INT = 10f * FT;
    static readonly float W_FAMILY_INT = 12.33f * FT; // 12'4"
    static readonly float D_FAMILY_INT = 15.5f * FT;  // 15'6"
    static readonly float W_HALLWAY_INT = 3.5f * FT;
    static readonly float D_HALLWAY_INT = 15f * FT;  // Estimated length
    static readonly float W_OFFICE_INT = 10f * FT;
    static readonly float D_OFFICE_INT = 9.167f * FT; // 9'2"
    static readonly float W_MAIN_BATH_INT = 5f * FT;
    static readonly float D_MAIN_BATH_INT = 8f * FT;
    static readonly float CLOSET_DEPTH_STD = 2f * FT;
    static readonly float ATTIC_HATCH_SIZE = 2.5f * FT;
    static readonly float PATIO_SLAB_THICKNESS = 0.5f * FT;

    // Master Suite Dimensions
    static readonly float W_MASTER_BED_INT = 12f * FT; // Blueprint: 12' (depth L-R on plan) - let's use this as Width
    static readonly float D_MASTER_BED_INT = 11f * FT; // Blueprint: 11' (width T-B on plan) - let's use this as Depth
    static readonly float W_WALKIN_CLOSET_INT = 6f * FT; // Estimate
    static readonly float D_WALKIN_CLOSET_INT = 5f * FT; // Estimate for one leg of L-shape
    static readonly float W_ENSUITE_BATH_INT = 8f * FT;  // Estimate
    static readonly float D_ENSUITE_BATH_INT = 6f * FT;  // Estimate

    // Garage Door Dimensions
    static readonly float GARAGE_DOOR_WIDTH = 9f * FT; // Typical single garage door width
    static readonly float GARAGE_DOOR_HEIGHT = 7f * FT;

    Material wallMat;
    Material floorMat;
    Material roofMat;
    Material glassMat; // For windows (optional)
    Material patioMat;

    Transform house;
    Transform mainFloor;
    Transform basement;
    Transform attic;
    Transform exteriorFeaturesGroup;
    

    // Helper struct to return dimensions from build methods
    public struct RoomDimensions
    {
        public float interiorWidth;
        public float interiorDepth;
        public float exteriorWidth; // Total span room occupies including its own walls
        public float exteriorDepth;
    }

     // Cached footprint for Basement/Attic
    private Rect mainFloorFootprint;
    private bool footprintCalculated = false;

    static Material MakeMat(Color c, bool transparent = false)
    {
        Material mat;
        if (transparent) {
            mat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse")); // Or Standard with transparency
        } else {
            mat = new Material(Shader.Find("Standard"));
        }
        mat.color = c;
        if (transparent) { // For Legacy Shaders/Transparent/Diffuse
            // Standard shader would require setting rendering mode (e.g., _Mode to 3 for Transparent)
            // and potentially using mat.SetOverrideTag("RenderType", "Transparent");
            // mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            // mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // mat.SetInt("_ZWrite", 0); // Don't write to depth buffer if fully transparent
            // mat.DisableKeyword("_ALPHATEST_ON");
            // mat.EnableKeyword("_ALPHABLEND_ON");
            // mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            // mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        return mat;
    }

    public void InitializeMaterials()
    {
        if (wallMat == null) wallMat = MakeMat(Color.gray);
        if (floorMat == null) floorMat = MakeMat(Color.white);
        if (roofMat == null) roofMat = MakeMat(Color.red);
        if (glassMat == null) glassMat = MakeMat(new Color(0.8f, 0.9f, 1f, 0.3f), true); // Example glass
        if (patioMat == null) patioMat = MakeMat(Color.lightGray * 0.8f); // Patio material
    }

    void Awake()
    {
        InitializeMaterials();
    }

    public void ExecuteBuild()
    {
        house = new GameObject("House").transform; house.SetParent(transform, false);
        mainFloor = new GameObject("MainFloor").transform; mainFloor.SetParent(house, false);
        basement = new GameObject("Basement").transform; basement.SetParent(house, false);
        attic = new GameObject("Attic").transform; attic.SetParent(house, false);
        exteriorFeaturesGroup = new GameObject("ExteriorFeatures").transform; exteriorFeaturesGroup.SetParent(house, false);
        footprintCalculated = false; // Reset footprint for new build

        // --- Room Positioning (baseCorner is bottom-left-FRONT of INTERIOR space) ---
        Vector3 garageBasePos = Vector3.zero;
        RoomDimensions garageDims = BuildGarageAt(garageBasePos);

        Vector3 foyerBasePos = new Vector3(garageBasePos.x + garageDims.interiorWidth + WALL_THICKNESS, 0, garageBasePos.z);
        RoomDimensions foyerDims = BuildFoyerAt(foyerBasePos);

        Vector3 livingBasePos = new Vector3(foyerBasePos.x + foyerDims.interiorWidth + WALL_THICKNESS, 0, foyerBasePos.z);
        RoomDimensions livingDims = BuildLivingRoomAt(livingBasePos);

        Vector3 diningBasePos = new Vector3(livingBasePos.x + livingDims.interiorWidth + WALL_THICKNESS, 0, livingBasePos.z);
        RoomDimensions diningDims = BuildDiningRoomAt(diningBasePos);

        Vector3 kitchenBasePos = new Vector3(diningBasePos.x + diningDims.interiorWidth + WALL_THICKNESS, 0, diningBasePos.z);
        RoomDimensions kitchenDims = BuildKitchenAt(kitchenBasePos);

        // Repositioned Stairs
        float stairsBaseX = diningBasePos.x + diningDims.interiorWidth + WALL_THICKNESS; // Aligned with Kitchen's start X
        float stairsBaseZ = diningBasePos.z + (diningDims.interiorDepth * 0.25f);
        Vector3 stairsBasePos = new Vector3(stairsBaseX, 0, stairsBaseZ);
        RoomDimensions stairsDims = BuildStairsAt(stairsBasePos);

        // Nook (Behind Kitchen)
        float kitchenBackWallEffectiveEndZ = kitchenBasePos.z + kitchenDims.interiorDepth + WALL_THICKNESS;
        float nookBaseX = kitchenBasePos.x + (kitchenDims.interiorWidth - W_NOOK_INT) * 0.5f;
        Vector3 nookBasePos = new Vector3(nookBaseX, 0, kitchenBackWallEffectiveEndZ);
        RoomDimensions nookDims = BuildNookAt(nookBasePos);

        // Family Room (Adjacent to Nook's "Top")
        float familyBaseZ = nookBasePos.z + nookDims.interiorDepth; // Assumes Nook top is boundary, not opening here
        float familyBaseX = nookBasePos.x + (nookDims.interiorWidth - W_FAMILY_INT) * 0.5f; // Center Family on Nook for now
        Vector3 familyBasePos = new Vector3(familyBaseX, 0, familyBaseZ);
        RoomDimensions familyDims = BuildFamilyRoomAt(familyBasePos);

        // Hallway (Extending from Family Room's left side)
        // Hallway runs along Z, to the left of Family Room. Opening on Family's -X wall.
        float hallwayStartX = familyBasePos.x - W_HALLWAY_INT - WALL_THICKNESS; // Hallway is left of Family's interior
        float hallwayStartZ = familyBasePos.z + (familyDims.interiorDepth - D_HALLWAY_INT) * 0.5f; // Center Hallway along Family's depth
        Vector3 hallwayBasePos = new Vector3(hallwayStartX, 0, hallwayStartZ);
        RoomDimensions hallwayDims = BuildHallwayAt(hallwayBasePos);

        // Office (Off Hallway - e.g., on the left side of Hallway, first door)
        float officeDoorOffsetZ_inHallway = D_HALLWAY_INT * 0.25f; // Door 25% down the hallway length
        Vector3 officeBasePos = new Vector3(
            hallwayBasePos.x - W_OFFICE_INT - WALL_THICKNESS, // Office is to the left of Hallway
            0,
            hallwayBasePos.z + officeDoorOffsetZ_inHallway - D_OFFICE_INT * 0.5f // Align center of Office depth with door
        );
        RoomDimensions officeDims = BuildOfficeAt(officeBasePos, officeDoorOffsetZ_inHallway);

        // Main Bathroom (Off Hallway - e.g., on the left side of Hallway, after Office door)
        float mainBathDoorOffsetZ_inHallway = D_HALLWAY_INT * 0.6f; // Door 60% down the hallway
        Vector3 mainBathBasePos = new Vector3(
            hallwayBasePos.x - W_MAIN_BATH_INT - WALL_THICKNESS, // Main Bath to the left of Hallway
            0,
            hallwayBasePos.z + mainBathDoorOffsetZ_inHallway - D_MAIN_BATH_INT * 0.5f // Align center of Bath depth with door
        );
        RoomDimensions mainBathDims = BuildMainBathroomAt(mainBathBasePos, mainBathDoorOffsetZ_inHallway);

        // Master Bedroom (At the end of the Hallway)
        Vector3 masterBedBasePos = new Vector3(
            hallwayBasePos.x + (W_HALLWAY_INT - W_MASTER_BED_INT) * 0.5f, // Centered on Hallway width
            0,
            hallwayBasePos.z + hallwayDims.interiorDepth + WALL_THICKNESS // At the end of Hallway
        );
        RoomDimensions masterBedDims = BuildMasterBedroomAt(masterBedBasePos);

        // Ensuite Master Bathroom (Adjacent to Master Bedroom)
        // E.g., on the "right" side of Master Bedroom (local +X)
        Vector3 ensuiteBasePos = new Vector3(
            masterBedBasePos.x + masterBedDims.interiorWidth + WALL_THICKNESS,
            0,
            masterBedBasePos.z + (masterBedDims.interiorDepth - D_ENSUITE_BATH_INT) * 0.5f // Align depth with Master
        );
        RoomDimensions ensuiteDims = BuildMasterBathroomAt(ensuiteBasePos);

        BuildCeilings();
        BuildBasement();
        BuildAtticAndRoof();
        BuildExteriorFeatures();
    }

    void Start() { ExecuteBuild(); }

    // --- Room Building Methods (Build<RoomName>At) ---
    RoomDimensions BuildGarageAt(Vector3 baseCorner) {
        float extW = W_GARAGE_INT + 2 * WALL_THICKNESS;
        float extD = D_GARAGE_INT + 2 * WALL_THICKNESS;
        var garage = new GameObject("Garage").transform; garage.SetParent(mainFloor, false); garage.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_GARAGE_INT * 0.5f, -FLOOR_THICKNESS * 0.5f, D_GARAGE_INT * 0.5f), new Vector3(W_GARAGE_INT, FLOOR_THICKNESS, D_GARAGE_INT), floorMat, garage);

        // Front Wall with Garage Door Opening
        var frontWall = BuildWallWithOpening("Front_Garage", new Vector3(W_GARAGE_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f),
            W_GARAGE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, /* has opening */
            W_GARAGE_INT * 0.5f, GARAGE_DOOR_WIDTH, GARAGE_DOOR_HEIGHT, garage);
        // Optional: Add a simple garage door panel (can be a thin cube)
        // CreateCube("GarageDoorPanel", Vector3.zero, new Vector3(GARAGE_DOOR_WIDTH, GARAGE_DOOR_HEIGHT, WALL_THICKNESS * 0.2f), MakeMat(Color.lightGray), frontWall.Find("Opening"));


        BuildSolidWall("Back_Garage", new Vector3(W_GARAGE_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_GARAGE_INT + WALL_THICKNESS * 0.5f), W_GARAGE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, garage);
        BuildSolidWall("Left_Garage", new Vector3(-WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_GARAGE_INT * 0.5f), D_GARAGE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, garage);
        
        // Right Wall (to Foyer) - Garage provides the opening.
        var rightWallGarage = BuildWallWithOpening("Right_Garage_to_Foyer", new Vector3(W_GARAGE_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_GARAGE_INT * 0.5f),
            D_GARAGE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, /* has opening */
            D_GARAGE_INT * 0.5f, DOOR_WIDTH, DOOR_HEIGHT, garage);
        // Foyer will place its door into this opening.

        return new RoomDimensions { interiorWidth = W_GARAGE_INT, interiorDepth = D_GARAGE_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildFoyerAt(Vector3 baseCorner) {
        float extW = W_FOYER_INT + 2 * WALL_THICKNESS; float extD = D_FOYER_INT + 2 * WALL_THICKNESS;
        var foyer = new GameObject("Foyer").transform; foyer.SetParent(mainFloor, false); foyer.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_FOYER_INT * 0.5f, -FLOOR_THICKNESS * 0.5f, D_FOYER_INT * 0.5f), new Vector3(W_FOYER_INT, FLOOR_THICKNESS, D_FOYER_INT), floorMat, foyer);

        var frontWall = BuildWallWithOpening("Front_Foyer", new Vector3(W_FOYER_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f), W_FOYER_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, W_FOYER_INT * 0.5f, DOOR_WIDTH, DOOR_HEIGHT, foyer);
        BuildDoor("EntryDoor", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS * 0.8f, Swing.InLeft, frontWall.Find("Opening"));
        
        // Back Wall (Foyer's Top wall on plan) - with Coat Closet
        float closetDoorWidth = DOOR_WIDTH * 1.5f; // Bi-fold or sliding often wider
        var backWall = BuildWallWithOpening("Back_Foyer_CoatCloset", new Vector3(W_FOYER_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_FOYER_INT + WALL_THICKNESS * 0.5f),
            W_FOYER_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, /* has opening */
            W_FOYER_INT * 0.3f /* Closet offset */, closetDoorWidth, DOOR_HEIGHT, foyer);
        BuildDoor("CoatClosetDoor", Vector3.zero, closetDoorWidth, DOOR_HEIGHT, WALL_THICKNESS * 0.5f, Swing.SlideLeft, backWall.Find("Opening"));
        // TODO: Add shallow closet recess box if desired

        // Left Wall (to Garage) - Foyer places the door into Garage's opening.
        Transform garageRightWallOpening = mainFloor.Find("Garage/Right_Garage_to_Foyer/Opening");
        if (garageRightWallOpening != null) {
            BuildDoor("Door_Foyer_to_Garage", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS * 0.8f, Swing.OutRight, garageRightWallOpening); // Swing OutRight from Foyer is Into Garage
        } else { Debug.LogWarning("Foyer: Could not find Garage opening for door."); }

        var rightWall = BuildWallWithOpening("Right_Foyer_to_Living", new Vector3(W_FOYER_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_FOYER_INT * 0.5f), D_FOYER_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_FOYER_INT * 0.5f, WIDE_CASED_OPENING_WIDTH, DOOR_HEIGHT, foyer);

        return new RoomDimensions { interiorWidth = W_FOYER_INT, interiorDepth = D_FOYER_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildLivingRoomAt(Vector3 baseCorner) {
        float extW = W_LIVING_INT + WALL_THICKNESS; float extD = D_LIVING_INT + 2 * WALL_THICKNESS;
        var living = new GameObject("LivingRoom").transform; living.SetParent(mainFloor, false); living.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_LIVING_INT * 0.5f, -FLOOR_THICKNESS * 0.5f, D_LIVING_INT * 0.5f), new Vector3(W_LIVING_INT, FLOOR_THICKNESS, D_LIVING_INT), floorMat, living);

        var frontWall = BuildWallWithOpening("Front_Living", new Vector3(W_LIVING_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f), W_LIVING_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, W_LIVING_INT * 0.5f, 8f * FT, 5f * FT, living, true);
        BuildWindow("FrontWindow_Living", Vector3.zero, 8f * FT, 5f * FT, WALL_THICKNESS * 0.5f, frontWall.Find("Opening"));
        BuildSolidWall("Back_Living", new Vector3(W_LIVING_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_LIVING_INT + WALL_THICKNESS * 0.5f), W_LIVING_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, living);
        // Left Wall: Assumed open/handled by Foyer's right wall opening
        var rightWall = BuildWallWithOpening("Right_Living_to_Dining", new Vector3(W_LIVING_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_LIVING_INT * 0.5f), D_LIVING_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_LIVING_INT * 0.5f, WIDE_CASED_OPENING_WIDTH, DOOR_HEIGHT, living);

        return new RoomDimensions { interiorWidth = W_LIVING_INT, interiorDepth = D_LIVING_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildDiningRoomAt(Vector3 baseCorner) {
        float extW = W_DINING_INT + WALL_THICKNESS; float extD = D_DINING_INT + 2 * WALL_THICKNESS;
        var dining = new GameObject("DiningRoom").transform; dining.SetParent(mainFloor, false); dining.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_DINING_INT * 0.5f, -FLOOR_THICKNESS * 0.5f, D_DINING_INT * 0.5f), new Vector3(W_DINING_INT, FLOOR_THICKNESS, D_DINING_INT), floorMat, dining);

        // ... (Front wall with window and sliding door as in C5) ...
        float halfFrontW = W_DINING_INT * 0.5f;
        var frontWindowSec = BuildWallWithOpening("FrontWindowSec_Dining", new Vector3(halfFrontW * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f), halfFrontW, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, halfFrontW * 0.5f, 3f * FT, 5f * FT, dining, true);
        BuildWindow("FrontWindow_Dining", Vector3.zero, 3f * FT, 5f * FT, WALL_THICKNESS * 0.5f, frontWindowSec.Find("Opening"));
        var frontDoorSec = BuildWallWithOpening("FrontDoorSec_Dining", new Vector3(halfFrontW + halfFrontW * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f), halfFrontW, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, halfFrontW * 0.5f, 6f * FT, DOOR_HEIGHT, dining);
        BuildDoor("SlidingDoor_Dining", Vector3.zero, 6f * FT, DOOR_HEIGHT, WALL_THICKNESS * 0.8f, Swing.SlideRight, frontDoorSec.Find("Opening"));

        // Back Wall (Dining's Top wall on plan) - with China Cabinet
        var backWallDining = BuildSolidWall("Back_Dining_ChinaCabinet", new Vector3(W_DINING_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_DINING_INT + WALL_THICKNESS * 0.5f), W_DINING_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, dining);
        // Add China Cabinet placeholder (simple protruding box)
        float cabinetW = W_DINING_INT * 0.4f; float cabinetH = 5f * FT; float cabinetD = 1f * FT;
        CreateCube("ChinaCabinet", new Vector3(W_DINING_INT * 0.7f - cabinetW*0.5f, cabinetH * 0.5f - FLOOR_THICKNESS, D_DINING_INT - cabinetD*0.5f + WALL_THICKNESS*0.5f), /* Local pos relative to dining */
                   new Vector3(cabinetW, cabinetH, cabinetD), MakeMat(Color.yellow), dining);


        var rightWall = BuildWallWithOpening("Right_Dining_to_Kitchen", new Vector3(W_DINING_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_DINING_INT * 0.5f), D_DINING_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_DINING_INT * 0.5f, WIDE_CASED_OPENING_WIDTH, DOOR_HEIGHT, dining);

        return new RoomDimensions { interiorWidth = W_DINING_INT, interiorDepth = D_DINING_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildKitchenAt(Vector3 baseCorner) {
        float extW = W_KITCHEN_INT + WALL_THICKNESS; float extD = D_KITCHEN_INT + 2 * WALL_THICKNESS;
        var kitchen = new GameObject("Kitchen").transform; kitchen.SetParent(mainFloor, false); kitchen.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_KITCHEN_INT * 0.5f, -FLOOR_THICKNESS * 0.5f, D_KITCHEN_INT * 0.5f), new Vector3(W_KITCHEN_INT, FLOOR_THICKNESS, D_KITCHEN_INT), floorMat, kitchen);

        var frontWall = BuildWallWithOpening("Front_Kitchen", new Vector3(W_KITCHEN_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f), W_KITCHEN_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, W_KITCHEN_INT * 0.5f, 4f * FT, 3f * FT, kitchen, true);
        BuildWindow("SinkWindow_Kitchen", Vector3.zero, 4f * FT, 3f * FT, WALL_THICKNESS * 0.5f, frontWall.Find("Opening"));
        var backWall = BuildWallWithOpening("Back_Kitchen_to_Nook", new Vector3(W_KITCHEN_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_KITCHEN_INT + WALL_THICKNESS * 0.5f), W_KITCHEN_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, W_KITCHEN_INT * 0.5f, W_KITCHEN_INT * 0.7f, MAIN_FLOOR_WALL_HEIGHT - COUNTER_HEIGHT, kitchen);
        // Left Wall: Assumed open/handled by Dining Room
        BuildSolidWall("Right_Kitchen", new Vector3(W_KITCHEN_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_KITCHEN_INT * 0.5f), D_KITCHEN_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, kitchen);

        // Counters & Appliances (local to kitchen)
        float sinkGap = 2.5f * FT; float cSegW = (W_KITCHEN_INT - sinkGap) * 0.5f;
        CreateCube("Counter_Front_L", new Vector3(cSegW*0.5f, COUNTER_HEIGHT*0.5f - FLOOR_THICKNESS, COUNTER_DEPTH*0.5f), new Vector3(cSegW, COUNTER_HEIGHT, COUNTER_DEPTH), floorMat, kitchen);
        CreateCube("Counter_Front_R", new Vector3(cSegW + sinkGap + cSegW*0.5f, COUNTER_HEIGHT*0.5f - FLOOR_THICKNESS, COUNTER_DEPTH*0.5f), new Vector3(cSegW, COUNTER_HEIGHT, COUNTER_DEPTH), floorMat, kitchen);
        CreateCube("Sink", new Vector3(W_KITCHEN_INT*0.5f, COUNTER_HEIGHT*0.5f - FLOOR_THICKNESS, COUNTER_DEPTH*0.5f), new Vector3(sinkGap, COUNTER_HEIGHT*0.9f, COUNTER_DEPTH), MakeMat(Color.blue), kitchen);
        CreateCube("Counter_Left", new Vector3(COUNTER_DEPTH*0.5f, COUNTER_HEIGHT*0.5f - FLOOR_THICKNESS, D_KITCHEN_INT*0.5f), new Vector3(COUNTER_DEPTH, COUNTER_HEIGHT, D_KITCHEN_INT - COUNTER_DEPTH*2f), floorMat, kitchen);
        CreateCube("Counter_Right_Peninsula", new Vector3(W_KITCHEN_INT-COUNTER_DEPTH*0.5f, COUNTER_HEIGHT*0.5f - FLOOR_THICKNESS, D_KITCHEN_INT*0.5f), new Vector3(COUNTER_DEPTH, COUNTER_HEIGHT, D_KITCHEN_INT*0.75f), floorMat, kitchen);
        CreateCube("Dishwasher", new Vector3(W_KITCHEN_INT*0.5f - sinkGap*0.5f - FT, COUNTER_HEIGHT*0.5f - FLOOR_THICKNESS, COUNTER_DEPTH*0.5f), new Vector3(2f*FT, COUNTER_HEIGHT, COUNTER_DEPTH), MakeMat(Color.cyan), kitchen);
        CreateCube("Fridge", new Vector3(W_KITCHEN_INT - FRIDGE_WIDTH*0.5f, FRIDGE_HEIGHT*0.5f - FLOOR_THICKNESS, D_KITCHEN_INT - FRIDGE_DEPTH*0.5f), new Vector3(FRIDGE_WIDTH,FRIDGE_HEIGHT,FRIDGE_DEPTH), MakeMat(Color.white*0.8f), kitchen);

        return new RoomDimensions { interiorWidth = W_KITCHEN_INT, interiorDepth = D_KITCHEN_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildStairsAt(Vector3 baseCorner) {
        int numRisers = Mathf.CeilToInt(MAIN_FLOOR_WALL_HEIGHT / STAIR_RISER_HEIGHT);
        float totalRunDepth = numRisers * STAIR_TREAD_DEPTH;
        float extW = STAIR_WIDTH + 2 * WALL_THICKNESS; float extD = totalRunDepth;
        var stairwell = new GameObject("Stairwell").transform; stairwell.SetParent(mainFloor, false); stairwell.localPosition = baseCorner;

        for (int i = 0; i < numRisers; i++) {
            CreateCube($"Step_{i + 1}", new Vector3(STAIR_WIDTH*0.5f, i*STAIR_RISER_HEIGHT + STAIR_RISER_HEIGHT*0.5f - FLOOR_THICKNESS, i*STAIR_TREAD_DEPTH + STAIR_TREAD_DEPTH*0.5f), new Vector3(STAIR_WIDTH, STAIR_RISER_HEIGHT, STAIR_TREAD_DEPTH), floorMat, stairwell);
        }
        BuildSolidWall("Guard_Left_Stairs", new Vector3(-WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, totalRunDepth * 0.5f), totalRunDepth, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, stairwell);
        BuildSolidWall("Guard_Right_Stairs", new Vector3(STAIR_WIDTH + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, totalRunDepth * 0.5f), totalRunDepth, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, stairwell);
        var voidMarker = new GameObject("CeilingVoid_Stairs").transform; voidMarker.SetParent(stairwell,false); voidMarker.localPosition = new Vector3(STAIR_WIDTH*0.5f, MAIN_FLOOR_WALL_HEIGHT, totalRunDepth*0.5f); voidMarker.localScale = new Vector3(STAIR_WIDTH, 1f, totalRunDepth);

        return new RoomDimensions { interiorWidth = STAIR_WIDTH, interiorDepth = totalRunDepth, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildNookAt(Vector3 baseCorner) {
        float extW = W_NOOK_INT + WALL_THICKNESS; float extD = D_NOOK_INT + WALL_THICKNESS;
        var nook = new GameObject("Nook").transform; nook.SetParent(mainFloor, false); nook.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_NOOK_INT*0.5f, -FLOOR_THICKNESS*0.5f, D_NOOK_INT*0.5f), new Vector3(W_NOOK_INT, FLOOR_THICKNESS, D_NOOK_INT), floorMat, nook);

        // Bottom Wall (-Z): Open to Kitchen
        var topWall = BuildWallWithOpening("Top_Nook_to_Family", new Vector3(W_NOOK_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_NOOK_INT+WALL_THICKNESS*0.5f), W_NOOK_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, W_NOOK_INT*0.5f, W_NOOK_INT*0.8f, DOOR_HEIGHT, nook);
        BuildSolidWall("Left_Nook", new Vector3(-WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_NOOK_INT*0.5f), D_NOOK_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, nook);
        var rightWall = BuildWallWithOpening("Right_Nook_to_Patio", new Vector3(W_NOOK_INT+WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_NOOK_INT*0.5f), D_NOOK_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_NOOK_INT*0.5f, DOOR_WIDTH, DOOR_HEIGHT, nook);
        BuildDoor("DoorToCoveredPatio", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS*0.8f, Swing.OutRight, rightWall.Find("Opening"));

        return new RoomDimensions { interiorWidth = W_NOOK_INT, interiorDepth = D_NOOK_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildFamilyRoomAt(Vector3 baseCorner) {
        float extW = W_FAMILY_INT + 2*WALL_THICKNESS; float extD = D_FAMILY_INT + WALL_THICKNESS;
        var family = new GameObject("FamilyRoom").transform; family.SetParent(mainFloor, false); family.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_FAMILY_INT*0.5f, -FLOOR_THICKNESS*0.5f, D_FAMILY_INT*0.5f), new Vector3(W_FAMILY_INT, FLOOR_THICKNESS, D_FAMILY_INT), floorMat, family);

        BuildSolidWall("Top_Family_to_MasterBed", new Vector3(W_FAMILY_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_FAMILY_INT+WALL_THICKNESS*0.5f), W_FAMILY_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, family);
        var leftWall = BuildWallWithOpening("Left_Family_to_Hallway", new Vector3(-WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_FAMILY_INT*0.5f), D_FAMILY_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_FAMILY_INT*0.3f, W_HALLWAY_INT, DOOR_HEIGHT, family);
        
        // Right Wall (Exterior - door AND window)
        // Strategy: Build wall in segments.
        // Segment 1: Solid part, Segment 2: Door opening, Segment 3: Solid part, Segment 4: Window opening, Segment 5: Solid part
        // Wall runs along Z, at local X = W_FAMILY_INT + WALL_THICKNESS*0.5f
        // Total length of this wall is D_FAMILY_INT.
        
        Transform familyRightWallContainer = new GameObject("Right_Family_Exterior_Container").transform;
        familyRightWallContainer.SetParent(family, false);
        familyRightWallContainer.localPosition = new Vector3(W_FAMILY_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_FAMILY_INT * 0.5f); // Center of the wall line

        float doorWidthFamily = DOOR_WIDTH;
        float windowWidthFamily = 6f * FT; // Large window
        float windowHeightFamily = 5f * FT;
        float solidSeg1Len = D_FAMILY_INT * 0.15f;
        float doorOffset = solidSeg1Len + doorWidthFamily * 0.5f;
        float solidSeg2Len = D_FAMILY_INT * 0.1f;
        float windowOffset = solidSeg1Len + doorWidthFamily + solidSeg2Len + windowWidthFamily * 0.5f;
        float solidSeg3Len = D_FAMILY_INT - (solidSeg1Len + doorWidthFamily + solidSeg2Len + windowWidthFamily);

        float currentZ = -D_FAMILY_INT * 0.5f; // Start from bottom of wall, local to container

        if (solidSeg1Len > 0.01f) {
            BuildSolidWall("FR_Right_Seg1", new Vector3(0, 0, currentZ + solidSeg1Len * 0.5f), solidSeg1Len, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, familyRightWallContainer);
            currentZ += solidSeg1Len;
        }
        var doorWallSeg = BuildWallWithOpening("FR_Right_DoorArea", new Vector3(0, 0, currentZ + doorWidthFamily * 0.5f), doorWidthFamily, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, doorWidthFamily * 0.5f, doorWidthFamily, DOOR_HEIGHT, familyRightWallContainer);
        BuildDoor("DoorToMainPatio", Vector3.zero, doorWidthFamily, DOOR_HEIGHT, WALL_THICKNESS*0.8f, Swing.OutRight, doorWallSeg.Find("Opening"));
        currentZ += doorWidthFamily;

        if (solidSeg2Len > 0.01f) {
            BuildSolidWall("FR_Right_Seg2", new Vector3(0, 0, currentZ + solidSeg2Len * 0.5f), solidSeg2Len, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, familyRightWallContainer);
            currentZ += solidSeg2Len;
        }
        var windowWallSeg = BuildWallWithOpening("FR_Right_WindowArea", new Vector3(0, 0, currentZ + windowWidthFamily * 0.5f), windowWidthFamily, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, windowWidthFamily*0.5f, windowWidthFamily, windowHeightFamily, familyRightWallContainer, true);
        BuildWindow("Window_FamilyPatio", Vector3.zero, windowWidthFamily, windowHeightFamily, WALL_THICKNESS*0.5f, windowWallSeg.Find("Opening"));
        currentZ += windowWidthFamily;
        
        if (solidSeg3Len > 0.01f) {
             BuildSolidWall("FR_Right_Seg3", new Vector3(0, 0, currentZ + solidSeg3Len * 0.5f), solidSeg3Len, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, familyRightWallContainer);
        }


        return new RoomDimensions { interiorWidth = W_FAMILY_INT, interiorDepth = D_FAMILY_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildHallwayAt(Vector3 baseCorner) {
        float extW = W_HALLWAY_INT + 2*WALL_THICKNESS; float extD = D_HALLWAY_INT + 2*WALL_THICKNESS;
        var hallway = new GameObject("Hallway").transform; hallway.SetParent(mainFloor, false); hallway.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_HALLWAY_INT*0.5f, -FLOOR_THICKNESS*0.5f, D_HALLWAY_INT*0.5f), new Vector3(W_HALLWAY_INT, FLOOR_THICKNESS, D_HALLWAY_INT), floorMat, hallway);

        // Right Wall (+X): Open to Family Room
        BuildSolidWall("Left_Hallway_Doors", new Vector3(-WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_HALLWAY_INT*0.5f), D_HALLWAY_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, hallway); // TODO: Doors to Office, Bath
        BuildSolidWall("FrontEnd_Hallway", new Vector3(W_HALLWAY_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, -WALL_THICKNESS*0.5f), W_HALLWAY_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, hallway);
        BuildSolidWall("BackEnd_Hallway", new Vector3(W_HALLWAY_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_HALLWAY_INT+WALL_THICKNESS*0.5f), W_HALLWAY_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, hallway); // TODO: Door to Master

        return new RoomDimensions { interiorWidth = W_HALLWAY_INT, interiorDepth = D_HALLWAY_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildOfficeAt(Vector3 baseCorner, float doorOffsetZInHallway) {
        float extW = W_OFFICE_INT + WALL_THICKNESS * 2f; float extD = D_OFFICE_INT + WALL_THICKNESS * 2f;
        var office = new GameObject("Office").transform; office.SetParent(mainFloor, false); office.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_OFFICE_INT*0.5f, -FLOOR_THICKNESS*0.5f, D_OFFICE_INT*0.5f), new Vector3(W_OFFICE_INT, FLOOR_THICKNESS, D_OFFICE_INT), floorMat, office);

        // Right Wall (local +X, Door to Hallway) - Office builds its door into opening provided by Hallway.
        // Hallway now makes the opening on its left wall. Office's right wall is effectively that opening.
        // So, Office doesn't build a structural right wall if Hallway provides the full opening.
        // Instead, it places its door frame.
        // This requires `hallway.Find("Left_Hall_OfficeDoorArea/Opening")` which is complex inter-room dependency.
        // Simpler: Hallway's left wall is solid. Office's right wall is BuildWallWithOpening + Door.
        // Let's stick to: Office builds its door connection. Hallway left wall needs corresponding opening.
        // The BuildHallwayAt now creates specific opening zones. Office should place its door into one.
        // Office's "Right_to_Hallway" wall IS the door. Find the corresponding Hallway opening group.
        Transform hallwayLeftWallOfficeOpening = mainFloor.Find($"Hallway/Left_Hall_OfficeDoorArea/Opening"); // May need better way to find
        if (hallwayLeftWallOfficeOpening != null) {
            BuildDoor("Door_Office_to_Hallway", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS*0.8f, Swing.InLeft, hallwayLeftWallOfficeOpening);
        } else {
            Debug.LogWarning("Could not find Hallway opening for Office door.");
            // Fallback: Office builds its own wall with door, assuming it aligns with solid part of Hallway
            var officeOwnRightWall = BuildWallWithOpening("Right_Office_to_Hallway_Fallback", new Vector3(W_OFFICE_INT+WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_OFFICE_INT*0.5f), D_OFFICE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_OFFICE_INT*0.5f, DOOR_WIDTH, DOOR_HEIGHT, office);
            BuildDoor("Door_Office_to_Hallway_fb", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS*0.8f, Swing.InLeft, officeOwnRightWall.Find("Opening"));
        }


        var leftWall = BuildWallWithOpening("Left_Office_Closet", new Vector3(-WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_OFFICE_INT*0.5f), D_OFFICE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_OFFICE_INT*0.25f, DOOR_WIDTH*0.8f, DOOR_HEIGHT, office);
        BuildDoor("ClosetDoor_Office", Vector3.zero, DOOR_WIDTH*0.8f, DOOR_HEIGHT, WALL_THICKNESS*0.5f, Swing.SlideLeft, leftWall.Find("Opening"));
        BuildSolidWall("Top_Office_to_MainBath", new Vector3(W_OFFICE_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_OFFICE_INT+WALL_THICKNESS*0.5f), W_OFFICE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, office);
        var bottomWall = BuildWallWithOpening("Bottom_Office_Exterior", new Vector3(W_OFFICE_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, -WALL_THICKNESS*0.5f), W_OFFICE_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true, W_OFFICE_INT*0.5f, 3*FT, 4*FT, office, true);
        BuildWindow("Window_Office", Vector3.zero, 3*FT, 4*FT, WALL_THICKNESS*0.5f, bottomWall.Find("Opening"));
        return new RoomDimensions { interiorWidth = W_OFFICE_INT, interiorDepth = D_OFFICE_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildMainBathroomAt(Vector3 baseCorner, float doorOffsetZInHallway) {
        float extW = W_MAIN_BATH_INT + 2*WALL_THICKNESS; float extD = D_MAIN_BATH_INT + 2*WALL_THICKNESS;
        var mainBath = new GameObject("MainBathroom").transform; mainBath.SetParent(mainFloor, false); mainBath.localPosition = baseCorner;
        CreateCube("Floor", new Vector3(W_MAIN_BATH_INT*0.5f, -FLOOR_THICKNESS*0.5f, D_MAIN_BATH_INT*0.5f), new Vector3(W_MAIN_BATH_INT, FLOOR_THICKNESS, D_MAIN_BATH_INT), floorMat, mainBath);

        Transform hallwayLeftWallBathOpening = mainFloor.Find($"Hallway/Left_Hall_MainBathDoorArea/Opening");
        if (hallwayLeftWallBathOpening != null) {
            BuildDoor("Door_MainBath_to_Hallway", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS*0.8f, Swing.InLeft, hallwayLeftWallBathOpening);
        } else {
             Debug.LogWarning("Could not find Hallway opening for Main Bathroom door.");
            var bathOwnRightWall = BuildWallWithOpening("Right_MainBath_to_Hallway_Fallback", new Vector3(W_MAIN_BATH_INT+WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_MAIN_BATH_INT*0.5f), D_MAIN_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_MAIN_BATH_INT*0.5f, DOOR_WIDTH, DOOR_HEIGHT, mainBath);
            BuildDoor("Door_MainBath_to_Hallway_fb", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS*0.8f, Swing.InLeft, bathOwnRightWall.Find("Opening"));
        }

        BuildSolidWall("Left_MainBath_to_Office", new Vector3(-WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_MAIN_BATH_INT*0.5f), D_MAIN_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, mainBath);
        BuildSolidWall("Top_MainBath_to_Master", new Vector3(W_MAIN_BATH_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_MAIN_BATH_INT+WALL_THICKNESS*0.5f), W_MAIN_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, mainBath);
        BuildSolidWall("Bottom_MainBath_Exterior", new Vector3(W_MAIN_BATH_INT*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, -WALL_THICKNESS*0.5f), W_MAIN_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, mainBath);
        return new RoomDimensions { interiorWidth = W_MAIN_BATH_INT, interiorDepth = D_MAIN_BATH_INT, exteriorWidth = extW, exteriorDepth = extD };
    }

    RoomDimensions BuildMasterBedroomAt(Vector3 baseCorner)
    {
        float extW = W_MASTER_BED_INT + 2 * WALL_THICKNESS;
        float extD = D_MASTER_BED_INT + 2 * WALL_THICKNESS;

        var masterBed = new GameObject("MasterBedroom").transform;
        masterBed.SetParent(mainFloor, false);
        masterBed.localPosition = baseCorner;

        CreateCube("Floor", new Vector3(W_MASTER_BED_INT * 0.5f, -FLOOR_THICKNESS * 0.5f, D_MASTER_BED_INT * 0.5f),
            new Vector3(W_MASTER_BED_INT, FLOOR_THICKNESS, D_MASTER_BED_INT), floorMat, masterBed);

        // Bottom Wall (local -Z, Door to Hallway)
        Transform hallwayBackEndOpening = mainFloor.Find($"Hallway/BackEnd_Hallway_to_Master/Opening");
        if (hallwayBackEndOpening != null) {
            BuildDoor("Door_Master_to_Hallway", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS * 0.8f, Swing.InRight, hallwayBackEndOpening);
        } else { Debug.LogWarning("MasterBed: Could not find Hallway opening for door."); }


        var topWall = BuildWallWithOpening("Top_Master_Exterior", new Vector3(W_MASTER_BED_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_MASTER_BED_INT + WALL_THICKNESS * 0.5f),
            W_MASTER_BED_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true,
            W_MASTER_BED_INT * 0.5f, W_MASTER_BED_INT * 0.4f, 4f * FT, masterBed, true);
        BuildWindow("Window_Master", Vector3.zero, W_MASTER_BED_INT * 0.4f, 4f * FT, WALL_THICKNESS * 0.5f, topWall.Find("Opening"));

        var rightWall = BuildWallWithOpening("Right_Master_to_Ensuite", new Vector3(W_MASTER_BED_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_MASTER_BED_INT * 0.5f),
            D_MASTER_BED_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true,
            D_MASTER_BED_INT * 0.7f, DOOR_WIDTH, DOOR_HEIGHT, masterBed);
        CreateCube("DisplayNook_Placeholder", new Vector3(0, MAIN_FLOOR_WALL_HEIGHT*0.6f, -D_MASTER_BED_INT*0.2f), new Vector3(WALL_THICKNESS*0.5f, 2*FT, 1.5f*FT), wallMat, rightWall);


        // Left Wall (local -X, part for Main Bathroom, part for Walk-in Closet door)
        // For simplicity, one opening for WIC. The shared wall with MainBath is assumed handled by MainBath build.
        // Let's assume WIC is in a corner, e.g., bottom-left of Master Bedroom.
        float wicDoorOffset = D_MASTER_BED_INT * 0.3f; // Door towards the "front" part of this wall
        var leftWall = BuildWallWithOpening("Left_Master_WIC_Access", new Vector3(-WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_MASTER_BED_INT * 0.5f),
            D_MASTER_BED_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, /* has opening */
            wicDoorOffset, DOOR_WIDTH, DOOR_HEIGHT, masterBed);
        BuildDoor("Door_To_WalkInCloset", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS * 0.8f, Swing.InLeft, leftWall.Find("Opening"));

        // Define Walk-In Closet area (L-shape is complex, doing a rectangle for now)
        // Positioned in the corner, e.g., local -X, local -Z from a point.
        // For now, just place the Attic Hatch marker. Assume closet is part of the MBR floor space.
        // Place Attic Hatch marker in ceiling of general closet area (e.g. near WIC door)
        float closetAreaX = W_WALKIN_CLOSET_INT * 0.5f; // From left wall of MBR
        float closetAreaZ = D_MASTER_BED_INT * 0.2f;   // Towards front of MBR
        
        var atticHatch = new GameObject("AtticHatch_MasterCloset").transform;
        atticHatch.SetParent(masterBed, false); // Child of Master Bedroom for now
        atticHatch.localPosition = new Vector3(closetAreaX, MAIN_FLOOR_WALL_HEIGHT, closetAreaZ); // On ceiling
        atticHatch.localScale = new Vector3(ATTIC_HATCH_SIZE, CEILING_THICKNESS, ATTIC_HATCH_SIZE); // Mark area
        CreateCube("AtticHatch_Visual", Vector3.zero, Vector3.one, MakeMat(Color.magenta), atticHatch);


        return new RoomDimensions {
            interiorWidth = W_MASTER_BED_INT, interiorDepth = D_MASTER_BED_INT,
            exteriorWidth = extW, exteriorDepth = extD
        };
    }

    RoomDimensions BuildMasterBathroomAt(Vector3 baseCorner) // Ensuite
    {
        float extW = W_ENSUITE_BATH_INT + 2 * WALL_THICKNESS;
        float extD = D_ENSUITE_BATH_INT + 2 * WALL_THICKNESS;

        var ensuite = new GameObject("MasterBathroom_Ensuite").transform;
        ensuite.SetParent(mainFloor, false);
        ensuite.localPosition = baseCorner;

        CreateCube("Floor", new Vector3(W_ENSUITE_BATH_INT * 0.5f, -FLOOR_THICKNESS * 0.5f, D_ENSUITE_BATH_INT * 0.5f),
            new Vector3(W_ENSUITE_BATH_INT, FLOOR_THICKNESS, D_ENSUITE_BATH_INT), floorMat, ensuite);

        // Left Wall (local -X, Shared with Master Bedroom for entry - Ensuite builds its door into Master's opening)
        Transform masterBedRightWallOpening = mainFloor.Find($"MasterBedroom/Right_Master_to_Ensuite/Opening");
        if (masterBedRightWallOpening != null) {
             BuildDoor("Door_Ensuite_to_Master", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS * 0.8f, Swing.InRight, masterBedRightWallOpening);
        } else {
            Debug.LogWarning("Could not find Master Bedroom opening for Ensuite door.");
            // Fallback
            var ensuiteOwnLeftWall = BuildWallWithOpening("Left_Ensuite_to_Master_Fallback", new Vector3(-WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_ENSUITE_BATH_INT*0.5f), D_ENSUITE_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, true, D_ENSUITE_BATH_INT*0.5f, DOOR_WIDTH, DOOR_HEIGHT, ensuite);
            BuildDoor("Door_Ensuite_to_Master_fb", Vector3.zero, DOOR_WIDTH, DOOR_HEIGHT, WALL_THICKNESS*0.8f, Swing.InRight, ensuiteOwnLeftWall.Find("Opening"));
        }
        // Blueprint "Left Wall (Shared with Master Bedroom Closet): Contains the sink/vanity."
        // This needs careful alignment if closet carves into bedroom. The door is on Right wall of MBR to get into ensuite.
        // For now, assuming Left wall is for Sink/Vanity and might be partly shared with WIC area.
        BuildSolidWall("Left_Ensuite_VanityWall", new Vector3(-WALL_THICKNESS*0.5f, MAIN_FLOOR_WALL_HEIGHT*0.5f, D_ENSUITE_BATH_INT*0.5f), D_ENSUITE_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, ensuite);
        // Placeholder: Sink/Vanity


        // Top Wall (local +Z, Exterior, Window)
        var topWall = BuildWallWithOpening("Top_Ensuite_Exterior", new Vector3(W_ENSUITE_BATH_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_ENSUITE_BATH_INT + WALL_THICKNESS * 0.5f),
            W_ENSUITE_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, true,
            W_ENSUITE_BATH_INT * 0.5f, 2f * FT, 3f * FT, ensuite, true /*isWindow*/);
        BuildWindow("Window_Ensuite", Vector3.zero, 2f*FT, 3f*FT, WALL_THICKNESS*0.5f, topWall.Find("Opening"));

        // Right Wall (local +X, Exterior or other)
        BuildSolidWall("Right_Ensuite_Exterior", new Vector3(W_ENSUITE_BATH_INT + WALL_THICKNESS * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, D_ENSUITE_BATH_INT * 0.5f),
            D_ENSUITE_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, false, ensuite);

        // Bottom Wall (local -Z, Shower/Tub, Toilet. Blueprint: "Shared with Family Room" - likely not with new layout)
        BuildSolidWall("Bottom_Ensuite_Fixtures", new Vector3(W_ENSUITE_BATH_INT * 0.5f, MAIN_FLOOR_WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f),
            W_ENSUITE_BATH_INT, MAIN_FLOOR_WALL_HEIGHT, WALL_THICKNESS, true, ensuite);
        // Placeholders: Shower/Tub cube, Toilet cube

        return new RoomDimensions {
            interiorWidth = W_ENSUITE_BATH_INT, interiorDepth = D_ENSUITE_BATH_INT,
            exteriorWidth = extW, exteriorDepth = extD
        };
    }


    // --- Helper Methods ---
    GameObject CreateCube(string name, Vector3 localCentre, Vector3 size, Material mat, Transform parent) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCentre; // Assumes localCentre is relative to parent's origin
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().material = mat;
        return go;
    }

    GameObject BuildSolidWall(string name, Vector3 wallCenterLocalPos, float length, float height, float thick, bool alongX, Transform parentRoom) {
        var wallGroup = new GameObject(name).transform;
        wallGroup.SetParent(parentRoom, false);
        wallGroup.localPosition = wallCenterLocalPos; // Position the group at the wall's intended center
        Vector3 cubeSize = alongX ? new Vector3(length, height, thick) : new Vector3(thick, height, length);
        CreateCube("Segment", Vector3.zero, cubeSize, wallMat, wallGroup); // Cube is centered within the group
        return wallGroup.gameObject;
    }

    Transform BuildWallWithOpening(string name, Vector3 wallCenterLocalPos, float length, float height, float thick, bool alongX, bool hasOpening, float openCenterOffset, float openWidth, float openHeight, Transform parentRoom, bool isWindow = false) {
        var wallGroup = new GameObject(name).transform;
        wallGroup.SetParent(parentRoom, false);
        wallGroup.localPosition = wallCenterLocalPos; // Wall group itself is centered

        var openingMarker = new GameObject("Opening").transform; // Create marker regardless
        openingMarker.SetParent(wallGroup, false);

        if (!hasOpening) {
            Vector3 solidSize = alongX ? new Vector3(length, height, thick) : new Vector3(thick, height, length);
            CreateCube("SolidSegment", Vector3.zero, solidSize, wallMat, wallGroup);
            openingMarker.localPosition = Vector3.zero; // Placeholder if no opening
            return wallGroup;
        }

        // All segment positions are local to the centered wallGroup
        float halfL = length * 0.5f; float halfH = height * 0.5f;

        // Opening boundaries, relative to wallGroup center
        float openMinX_local = alongX ? (openCenterOffset - openWidth * 0.5f - halfL) : (-thick * 0.5f);
        float openMaxX_local = alongX ? (openCenterOffset + openWidth * 0.5f - halfL) : (thick * 0.5f);
        float openMinZ_local = alongX ? (-thick * 0.5f) : (openCenterOffset - openWidth * 0.5f - halfL);
        float openMaxZ_local = alongX ? (thick * 0.5f) : (openCenterOffset + openWidth * 0.5f - halfL);

        float openBottomY_local = (isWindow ? (2.5f * FT - halfH) : -halfH); // y from floor of wall group
        float openTopY_local = openBottomY_local + openHeight;

        // Position opening marker
        if (alongX) openingMarker.localPosition = new Vector3(openCenterOffset - halfL, openBottomY_local + openHeight * 0.5f, 0);
        else openingMarker.localPosition = new Vector3(0, openBottomY_local + openHeight * 0.5f, openCenterOffset - halfL);


        // Segment 1 (Left of opening OR Bottom part of wall if vertical)
        if (alongX) {
            float seg1Len = (openCenterOffset - halfL) - (-halfL) - openWidth * 0.5f; // length from wall start to opening start
            seg1Len = openCenterOffset - openWidth * 0.5f; // Corrected: openCenter is from wall start
            if (seg1Len > 0.01f) CreateCube("SegLeft", new Vector3(-halfL + seg1Len * 0.5f, 0, 0), new Vector3(seg1Len, height, thick), wallMat, wallGroup);
        } else { // alongZ
            float seg1Len = openCenterOffset - openWidth * 0.5f;
            if (seg1Len > 0.01f) CreateCube("SegLeft", new Vector3(0, 0, -halfL + seg1Len * 0.5f), new Vector3(thick, height, seg1Len), wallMat, wallGroup);
        }

        // Segment 2 (Right of opening)
        if (alongX) {
            float seg2Start = openCenterOffset + openWidth * 0.5f;
            float seg2Len = length - seg2Start;
            if (seg2Len > 0.01f) CreateCube("SegRight", new Vector3(-halfL + seg2Start + seg2Len * 0.5f, 0, 0), new Vector3(seg2Len, height, thick), wallMat, wallGroup);
        } else { // alongZ
            float seg2Start = openCenterOffset + openWidth * 0.5f;
            float seg2Len = length - seg2Start;
            if (seg2Len > 0.01f) CreateCube("SegRight", new Vector3(0, 0, -halfL + seg2Start + seg2Len * 0.5f), new Vector3(thick, height, seg2Len), wallMat, wallGroup);
        }

        // Segment 3 (Above opening)
        float seg3Height = halfH - openTopY_local;
        if (seg3Height > 0.01f) {
            float seg3CenterY = openTopY_local + seg3Height * 0.5f;
            if (alongX) CreateCube("SegAbove", new Vector3(openCenterOffset - halfL, seg3CenterY, 0), new Vector3(openWidth, seg3Height, thick), wallMat, wallGroup);
            else CreateCube("SegAbove", new Vector3(0, seg3CenterY, openCenterOffset - halfL), new Vector3(thick, seg3Height, openWidth), wallMat, wallGroup);
        }

        // Segment 4 (Below opening, for windows)
        float seg4Height = openBottomY_local - (-halfH);
        if (isWindow && seg4Height > 0.01f) {
            float seg4CenterY = -halfH + seg4Height * 0.5f;
            if (alongX) CreateCube("SegBelow", new Vector3(openCenterOffset - halfL, seg4CenterY, 0), new Vector3(openWidth, seg4Height, thick), wallMat, wallGroup);
            else CreateCube("SegBelow", new Vector3(0, seg4CenterY, openCenterOffset - halfL), new Vector3(thick, seg4Height, openWidth), wallMat, wallGroup);
        }
        return wallGroup;
    }

    GameObject BuildDoor(string name, Vector3 localCentreInOpeningMarker, float width, float height, float panelThickness, Swing swing, Transform openingMarkerParent) {
        var doorPivot = new GameObject(name).transform;
        doorPivot.SetParent(openingMarkerParent, false);
        doorPivot.localPosition = localCentreInOpeningMarker; // Door pivot centered in the opening marker

        Vector3 panelLocalPos = Vector3.zero; // Relative to doorPivot
        // Adjust panel position so it hinges correctly
        switch (swing) {
            case Swing.InLeft: case Swing.OutLeft: panelLocalPos.x = width * 0.5f; break;
            case Swing.InRight: case Swing.OutRight: panelLocalPos.x = -width * 0.5f; break;
        }
        if (swing == Swing.OutLeft || swing == Swing.OutRight) {
            doorPivot.localRotation = Quaternion.Euler(0, 180f, 0); // Pre-rotate for outward swing
        }
        CreateCube("Panel", panelLocalPos, new Vector3(width, height, panelThickness), wallMat, doorPivot);
        return doorPivot.gameObject;
    }

    GameObject BuildWindow(string name, Vector3 localCentreInOpeningMarker, float w, float h, float frameElementThickness, Transform openingMarkerParent) {
        var windowAssembly = new GameObject(name).transform;
        windowAssembly.SetParent(openingMarkerParent, false);
        windowAssembly.localPosition = localCentreInOpeningMarker;

        float halfW = w * 0.5f; float halfH = h * 0.5f; float t = frameElementThickness;
        CreateCube("LeftFrame", new Vector3(-halfW + t * 0.5f, 0, 0), new Vector3(t, h, t), wallMat, windowAssembly);
        CreateCube("RightFrame", new Vector3(halfW - t * 0.5f, 0, 0), new Vector3(t, h, t), wallMat, windowAssembly);
        CreateCube("TopFrame", new Vector3(0, halfH - t * 0.5f, 0), new Vector3(w - 2 * t, t, t), wallMat, windowAssembly);
        CreateCube("BottomFrame", new Vector3(0, -halfH + t * 0.5f, 0), new Vector3(w - 2 * t, t, t), wallMat, windowAssembly);
        // Optional Glass Pane
        CreateCube("GlassPane", Vector3.zero, new Vector3(w - 2*t, h - 2*t, t*0.25f), glassMat, windowAssembly);
        return windowAssembly.gameObject;
    }

    void AddCeiling(Transform roomTransform, float roomIntWidth, float roomIntDepth, float ceilingYPosLocalToRoom) {
        Vector3 ceilingLocalCenter = new Vector3(roomIntWidth * 0.5f, ceilingYPosLocalToRoom, roomIntDepth * 0.5f);
        Vector3 ceilingSize = new Vector3(roomIntWidth, CEILING_THICKNESS, roomIntDepth);
        CreateCube("Ceiling", ceilingLocalCenter, ceilingSize, floorMat, roomTransform);
    }

    void CalculateMainFloorFootprint() {
        if (mainFloor.childCount == 0) {
            Debug.LogWarning("MainFloor has no rooms to calculate footprint from.");
            mainFloorFootprint = new Rect(0, 0, 30 * FT, 30 * FT); // Default fallback
            footprintCalculated = true;
            return;
        }

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (Transform roomTransform in mainFloor) {
            // Assuming roomTransform.localPosition is the baseCorner (bottom-left-front of interior)
            // And a "Floor" child's localScale gives interior dimensions
            var floorObj = roomTransform.Find("Floor");
            if (floorObj != null) {
                Vector3 roomBase = roomTransform.localPosition;
                Vector3 floorScale = floorObj.localScale; // Assumed to be interior W, H(thickness), D

                float roomMinX = roomBase.x - WALL_THICKNESS; // Include outer edge of left wall
                float roomMaxX = roomBase.x + floorScale.x + WALL_THICKNESS; // Include outer edge of right wall
                float roomMinZ = roomBase.z - WALL_THICKNESS; // Include outer edge of front wall
                float roomMaxZ = roomBase.z + floorScale.z + WALL_THICKNESS; // Include outer edge of back wall
                
                // For rooms that only build one side wall (e.g. LivingRoom, DiningRoom, Kitchen in current setup)
                // their effective exterior span needs to be correctly determined.
                // This simplistic addition of WALL_THICKNESS might not always be right if rooms already account for shared walls.
                // A more robust way would be for each Build<RoomName>At to return its true world AABB.
                // For now, using this approximation based on floor scale and base position.

                minX = Mathf.Min(minX, roomMinX);
                maxX = Mathf.Max(maxX, roomMaxX);
                minZ = Mathf.Min(minZ, roomMinZ);
                maxZ = Mathf.Max(maxZ, roomMaxZ);
            } else if (roomTransform.name.Contains("Stairs")) { // Special handling for stairs
                 Vector3 roomBase = roomTransform.localPosition;
                 RoomDimensions dims = new RoomDimensions(); // Need actual stairs dimensions
                 // temp: get from BuildStairsAt
                 int numRisers = Mathf.CeilToInt(MAIN_FLOOR_WALL_HEIGHT / STAIR_RISER_HEIGHT);
                 dims.interiorDepth = numRisers * STAIR_TREAD_DEPTH;
                 dims.interiorWidth = STAIR_WIDTH;
                 dims.exteriorWidth = dims.interiorWidth + 2 * WALL_THICKNESS;
                 dims.exteriorDepth = dims.interiorDepth;


                minX = Mathf.Min(minX, roomBase.x - WALL_THICKNESS);
                maxX = Mathf.Max(maxX, roomBase.x + dims.interiorWidth + WALL_THICKNESS);
                minZ = Mathf.Min(minZ, roomBase.z); // Stairs start at their Z
                maxZ = Mathf.Max(maxZ, roomBase.z + dims.interiorDepth);
            }
        }
        mainFloorFootprint = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        footprintCalculated = true;
        Debug.Log($"MainFloor Footprint: {mainFloorFootprint}");
    }

    void BuildCeilings() {
        if (!footprintCalculated) CalculateMainFloorFootprint();

        float ceilingY = MAIN_FLOOR_WALL_HEIGHT - CEILING_THICKNESS * 0.5f;
        foreach (Transform roomTransform in mainFloor) {
            if (roomTransform.Find("CeilingVoid_Stairs") != null) continue;
            
            var atticHatch = roomTransform.Find("AtticHatch_MasterCloset"); // Check if this room has the hatch

            var floorObj = roomTransform.Find("Floor");
            if (floorObj != null) {
                if (atticHatch != null) {
                    // Build ceiling with a hole for the attic hatch
                    // This is complex: requires building ceiling in segments around the hatch.
                    // For now, we'll create a simple void marker like for stairs,
                    // or just build the full ceiling and place the hatch visual on top.
                    // For simplicity, let's let BuildCeilings make a full ceiling,
                    // and the AtticHatch_MasterCloset GameObject itself serves as the visual marker.
                    // Or, we can refine `AddCeiling` to take a list of void Rects.
                    Debug.Log($"Attic hatch found in {roomTransform.name}, full ceiling being built for now.");
                }
                AddCeiling(roomTransform, floorObj.localScale.x, floorObj.localScale.z, ceilingY);

            } else if (!roomTransform.name.Contains("Stairs")) { // Stairs don't have a main "Floor" child
                Debug.LogWarning("Could not find floor to determine ceiling size for: " + roomTransform.name);
            }
        }
    }

    void BuildBasement() {
        if (!footprintCalculated) CalculateMainFloorFootprint();

        float fpWidth = mainFloorFootprint.width;
        float fpDepth = mainFloorFootprint.height;
        Vector3 fpMin = new Vector3(mainFloorFootprint.xMin, 0, mainFloorFootprint.yMin);

        Vector3 basementFloorLevelBase = new Vector3(fpMin.x, -FLOOR_HEIGHT - FLOOR_THICKNESS, fpMin.z);
        basement.localPosition = new Vector3(mainFloorFootprint.xMin, -FLOOR_HEIGHT - FLOOR_THICKNESS, mainFloorFootprint.yMin);

        // Floor (local to basement group)
        CreateCube("Floor_Basement", new Vector3(fpWidth*0.5f, FLOOR_THICKNESS*0.5f, fpDepth*0.5f), new Vector3(fpWidth, FLOOR_THICKNESS, fpDepth), floorMat, basement);
        
        // Walls (local to basement group)
        BuildSolidWall("Front_Basement", new Vector3(fpWidth*0.5f, FLOOR_HEIGHT*0.5f + FLOOR_THICKNESS*0.5f, WALL_THICKNESS*0.5f), fpWidth, FLOOR_HEIGHT, WALL_THICKNESS, true, basement);
        BuildSolidWall("Back_Basement", new Vector3(fpWidth*0.5f, FLOOR_HEIGHT*0.5f + FLOOR_THICKNESS*0.5f, fpDepth - WALL_THICKNESS*0.5f), fpWidth, FLOOR_HEIGHT, WALL_THICKNESS, true, basement);
        BuildSolidWall("Left_Basement", new Vector3(WALL_THICKNESS*0.5f, FLOOR_HEIGHT*0.5f + FLOOR_THICKNESS*0.5f, fpDepth*0.5f), fpDepth, FLOOR_HEIGHT, WALL_THICKNESS, false, basement);
        BuildSolidWall("Right_Basement", new Vector3(fpWidth - WALL_THICKNESS*0.5f, FLOOR_HEIGHT*0.5f + FLOOR_THICKNESS*0.5f, fpDepth*0.5f), fpDepth, FLOOR_HEIGHT, WALL_THICKNESS, false, basement);
    
        // Breaker Box Placeholder
        CreateCube("BreakerBox_Placeholder", new Vector3(fpWidth * 0.1f, FLOOR_HEIGHT * 0.6f, WALL_THICKNESS * 1.5f), new Vector3(1*FT, 2*FT, 0.5f*FT), MakeMat(Color.darkGray), basement);
    }

    void BuildAtticAndRoof() {
        if (!footprintCalculated) CalculateMainFloorFootprint();

        float fpWidth = mainFloorFootprint.width;
        float fpDepth = mainFloorFootprint.height;
        Vector3 fpMin = new Vector3(mainFloorFootprint.xMin, 0, mainFloorFootprint.yMin);

        Vector3 atticBaseWorldPos = new Vector3(fpMin.x, MAIN_FLOOR_WALL_HEIGHT, fpMin.z);
        attic.position = atticBaseWorldPos; // Position attic group

        // Attic Walls (local to attic group)
        BuildSolidWall("LeftWall_Attic", new Vector3(WALL_THICKNESS * 0.5f, ATTIC_WALL_HEIGHT*0.5f, fpDepth*0.5f), fpDepth, ATTIC_WALL_HEIGHT,WALL_THICKNESS,false,attic);
        BuildSolidWall("RightWall_Attic", new Vector3(fpWidth - WALL_THICKNESS*0.5f, ATTIC_WALL_HEIGHT*0.5f, fpDepth*0.5f), fpDepth, ATTIC_WALL_HEIGHT,WALL_THICKNESS,false,attic);
        
        // Front Attic Wall (local -Z, if fpMin.z is "front" of house) - Add Window Here
        var frontAtticWall = BuildWallWithOpening("FrontWall_Attic", new Vector3(fpWidth*0.5f, ATTIC_WALL_HEIGHT*0.5f, WALL_THICKNESS*0.5f), 
            fpWidth, ATTIC_WALL_HEIGHT,WALL_THICKNESS,true, true, /* has opening */
            fpWidth * 0.5f, 3*FT, 2.5f*FT, attic, true /* isWindow */);
        BuildWindow("Window_AtticFront", Vector3.zero, 3*FT, 2.5f*FT, WALL_THICKNESS*0.5f, frontAtticWall.Find("Opening"));

        BuildSolidWall("BackWall_Attic", new Vector3(fpWidth*0.5f, ATTIC_WALL_HEIGHT*0.5f, fpDepth-WALL_THICKNESS*0.5f), fpWidth, ATTIC_WALL_HEIGHT,WALL_THICKNESS,true,attic);
        
        // Roof (local to attic group)
        float halfRoofSpan = fpWidth*0.5f;      
        float roofPanelSlopeLength = Mathf.Sqrt(halfRoofSpan*halfRoofSpan + ROOF_RISE*ROOF_RISE);
        float roofAngleDegrees = Mathf.Atan2(ROOF_RISE, halfRoofSpan) * Mathf.Rad2Deg;

        Vector3 leftRoofPanelCenter = new Vector3(fpWidth * 0.25f, ATTIC_WALL_HEIGHT + ROOF_RISE * 0.5f, fpDepth * 0.5f);
        var leftRoof = CreateCube("Roof_Left", leftRoofPanelCenter, new Vector3(fpDepth, WALL_THICKNESS, roofPanelSlopeLength), roofMat, attic);
        leftRoof.transform.localRotation = Quaternion.Euler(roofAngleDegrees, 0f, 0f);

        Vector3 rightRoofPanelCenter = new Vector3(fpWidth * 0.75f, ATTIC_WALL_HEIGHT + ROOF_RISE * 0.5f, fpDepth * 0.5f);
        var rightRoof = CreateCube("Roof_Right", rightRoofPanelCenter, new Vector3(fpDepth, WALL_THICKNESS, roofPanelSlopeLength), roofMat, attic);
        rightRoof.transform.localRotation = Quaternion.Euler(-roofAngleDegrees, 0f, 0f);
    }

    void BuildAllExteriorFeatures() {
        BuildCoveredEntry();
        BuildPatios();
    }

    void BuildCoveredEntry() {
        var foyerTR = mainFloor.Find("Foyer");
        if (foyerTR == null) { Debug.LogError("Foyer not found for Covered Entry."); return; }
        var foyerFloor = foyerTR.Find("Floor");
        if (foyerFloor == null) { Debug.LogError("Foyer floor not found for Covered Entry."); return; }

        float foyerInteriorWidth = foyerFloor.localScale.x;
        Vector3 foyerWorldPos = foyerTR.position; // This is Foyer's interior bottom-left-front

        float coverDepth = 4f * FT;
        float coverWidth = foyerInteriorWidth + 2 * WALL_THICKNESS; // Make cover slightly wider than interior

        var entryCoverGroup = new GameObject("CoveredEntry").transform;
        entryCoverGroup.SetParent(exteriorFeaturesGroup, false); // Parent to the main exterior group

        // Base for the covered entry, in front of Foyer's front wall plane
        Vector3 coverBaseWorld = new Vector3(
            foyerWorldPos.x - WALL_THICKNESS, // Align with Foyer's effective exterior start
            foyerWorldPos.y,
            foyerWorldPos.z - coverDepth - WALL_THICKNESS // Positioned in front
        );
        entryCoverGroup.position = coverBaseWorld;

        // Elements local to entryCoverGroup
        CreateCube("Slab_CoveredEntry", new Vector3(coverWidth * 0.5f, -PATIO_SLAB_THICKNESS * 0.5f, coverDepth * 0.5f),
                   new Vector3(coverWidth, PATIO_SLAB_THICKNESS, coverDepth), patioMat, entryCoverGroup);
        CreateCube("Column_Left_Entry", new Vector3(WALL_THICKNESS * 0.5f, FLOOR_HEIGHT * 0.5f, coverDepth - WALL_THICKNESS * 0.5f),
                   new Vector3(WALL_THICKNESS, FLOOR_HEIGHT, WALL_THICKNESS), wallMat, entryCoverGroup);
        CreateCube("Column_Right_Entry", new Vector3(coverWidth - WALL_THICKNESS * 0.5f, FLOOR_HEIGHT * 0.5f, coverDepth - WALL_THICKNESS * 0.5f),
                   new Vector3(WALL_THICKNESS, FLOOR_HEIGHT, WALL_THICKNESS), wallMat, entryCoverGroup);
        CreateCube("Roof_CoveredEntry", new Vector3(coverWidth * 0.5f, FLOOR_HEIGHT + WALL_THICKNESS * 0.5f, coverDepth * 0.5f),
                   new Vector3(coverWidth, WALL_THICKNESS, coverDepth), roofMat, entryCoverGroup);
    }

    void BuildPatios() {
        // --- Main Patio (accessible from Family Room) ---
        var familyRoomTR = mainFloor.Find("FamilyRoom");
        if (familyRoomTR != null) {
            // Find the door to the main patio on the Family Room's right wall
            // We need the world position of this door's threshold.
            // This is tricky without direct reference. For now, approximate from Family Room's position and dimensions.
            Vector3 familyRoomBaseWorld = familyRoomTR.position;
            // Assume door is on Family Room's +X side (its right interior wall)
            float patioStartX = familyRoomBaseWorld.x + W_FAMILY_INT + WALL_THICKNESS;
            float patioStartZ = familyRoomBaseWorld.z + D_FAMILY_INT * 0.1f; // Start near door
            float patioWidth = 15f * FT; // Example size
            float patioDepth = 10f * FT;

            var mainPatio = CreateCube("MainPatio_Slab",
                new Vector3(patioStartX + patioWidth * 0.5f, -PATIO_SLAB_THICKNESS * 0.5f, patioStartZ + patioDepth * 0.5f),
                new Vector3(patioWidth, PATIO_SLAB_THICKNESS, patioDepth), patioMat, exteriorFeaturesGroup);
            mainPatio.transform.position = new Vector3(patioStartX, -PATIO_SLAB_THICKNESS * 0.5f, patioStartZ); // Adjust to use base
             mainPatio.transform.localPosition = new Vector3(patioStartX - house.position.x, -PATIO_SLAB_THICKNESS * 0.5f, patioStartZ - house.position.z); // Relative to house
             mainPatio.transform.SetParent(exteriorFeaturesGroup, true); // Set parent while preserving world pos
             mainPatio.transform.localPosition = new Vector3(patioStartX, -PATIO_SLAB_THICKNESS * 0.5f, patioStartZ); // this might be better if exteriorFeaturesGroup is at 0,0,0

        } else { Debug.LogWarning("FamilyRoom not found for Main Patio."); }

        // --- Covered Patio (accessible from Nook) ---
        var nookTR = mainFloor.Find("Nook");
        if (nookTR != null) {
            Vector3 nookBaseWorld = nookTR.position;
            // Assume door is on Nook's +X side (its right interior wall)
            float coveredPatioStartX = nookBaseWorld.x + W_NOOK_INT + WALL_THICKNESS;
            float coveredPatioStartZ = nookBaseWorld.z + D_NOOK_INT * 0.25f; // Example Z alignment
            float coveredPatioW = 8f * FT; // Example size
            float coveredPatioD = 6f * FT;

            var coveredPatioGroup = new GameObject("CoveredPatio_Nook").transform;
            coveredPatioGroup.SetParent(exteriorFeaturesGroup, false);
            coveredPatioGroup.position = new Vector3(coveredPatioStartX, 0, coveredPatioStartZ); // World position

            CreateCube("Slab_NookPatio", new Vector3(coveredPatioW * 0.5f, -PATIO_SLAB_THICKNESS * 0.5f, coveredPatioD * 0.5f),
                       new Vector3(coveredPatioW, PATIO_SLAB_THICKNESS, coveredPatioD), patioMat, coveredPatioGroup);
            // Columns (similar to Covered Entry, but fewer/different placement)
            CreateCube("Column1_NookPatio", new Vector3(WALL_THICKNESS*0.5f, FLOOR_HEIGHT*0.5f, coveredPatioD - WALL_THICKNESS*0.5f),
                       new Vector3(WALL_THICKNESS, FLOOR_HEIGHT, WALL_THICKNESS), wallMat, coveredPatioGroup);
            CreateCube("Column2_NookPatio", new Vector3(coveredPatioW - WALL_THICKNESS*0.5f, FLOOR_HEIGHT*0.5f, coveredPatioD - WALL_THICKNESS*0.5f),
                       new Vector3(WALL_THICKNESS, FLOOR_HEIGHT, WALL_THICKNESS), wallMat, coveredPatioGroup);
            CreateCube("Roof_NookPatio", new Vector3(coveredPatioW * 0.5f, FLOOR_HEIGHT + WALL_THICKNESS*0.5f, coveredPatioD*0.5f),
                       new Vector3(coveredPatioW, WALL_THICKNESS, coveredPatioD), roofMat, coveredPatioGroup);

        } else { Debug.LogWarning("Nook not found for Covered Patio."); }
    }
    
    void BuildExteriorFeatures() {
        var foyerTR = mainFloor.Find("Foyer"); 
        if(foyerTR == null) { Debug.LogError("Foyer not found for ExteriorFeatures."); return; }
        
        // Get Foyer's interior width for the cover. Assuming Foyer's floor child scale represents this.
        var foyerFloor = foyerTR.Find("Floor");
        if (foyerFloor == null) { Debug.LogError("Foyer floor not found for ExteriorFeatures."); return; }
        float foyerInteriorWidth = foyerFloor.localScale.x; 
        
        Vector3 foyerWorldPos = foyerTR.position; // This is the Foyer's interior bottom-left-front corner
        
        float coverDepth = 4f*FT;
        var entryCoverGroup = new GameObject("EntryCover").transform; entryCoverGroup.SetParent(house,false);
        
        // Base corner for entry cover, in front of foyer's world position, aligned with its width
        Vector3 coverBaseWorld = new Vector3(foyerWorldPos.x, foyerWorldPos.y, foyerWorldPos.z - coverDepth - WALL_THICKNESS); 
        entryCoverGroup.position = coverBaseWorld;

        // Elements local to entryCoverGroup
        CreateCube("Slab_EntryCover", new Vector3(foyerInteriorWidth*0.5f, -FLOOR_THICKNESS*0.5f, coverDepth*0.5f), new Vector3(foyerInteriorWidth, FLOOR_THICKNESS, coverDepth), floorMat, entryCoverGroup);
        CreateCube("Column_Left_Entry", new Vector3(WALL_THICKNESS*0.5f, FLOOR_HEIGHT*0.5f, coverDepth - WALL_THICKNESS*0.5f), new Vector3(WALL_THICKNESS, FLOOR_HEIGHT, WALL_THICKNESS), wallMat, entryCoverGroup);
        CreateCube("Column_Right_Entry", new Vector3(foyerInteriorWidth - WALL_THICKNESS*0.5f, FLOOR_HEIGHT*0.5f, coverDepth - WALL_THICKNESS*0.5f), new Vector3(WALL_THICKNESS, FLOOR_HEIGHT, WALL_THICKNESS), wallMat, entryCoverGroup);
        CreateCube("Roof_EntryCover", new Vector3(foyerInteriorWidth*0.5f, FLOOR_HEIGHT + WALL_THICKNESS*0.5f, coverDepth*0.5f), new Vector3(foyerInteriorWidth, WALL_THICKNESS, coverDepth), roofMat, entryCoverGroup);
    }

#if UNITY_EDITOR
    [MenuItem("House/Rebuild")]
    static void Rebuild() {
        var old = GameObject.Find("House");
        if (old) DestroyImmediate(old);
        var builder = FindObjectOfType<AdvancedHouseBuilder>();
        if (builder == null) {
            var builderGO = new GameObject("AdvancedHouseBuilder");
            builder = builderGO.AddComponent<AdvancedHouseBuilder>();
        }
        builder.InitializeMaterials(); // Ensure materials are ready
        builder.ExecuteBuild(); 
        if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
    }
#endif
}