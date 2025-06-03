using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HousePlan", menuName = "House/House Plan")]
public class HousePlanSO : ScriptableObject
{
    public float storyHeight = 2.7f;
    public float exteriorWallThickness = 0.15f;
    public float interiorWallThickness = 0.1f;

    public List<RoomData> rooms;
    public List<DoorSpec> doors;
    public List<WindowSpec> windows;
    public List<OpeningSpec> openings; // For cased openings, passthroughs
}

[System.Serializable]
public struct RoomData
{
    public string roomId; // e.g., "Foyer", "LivingRoom"
    public string roomLabel; // e.g., "Foyer", "Living Room"
    public Vector2 dimensions; // (width, depth) in meters
    public Vector3 position; // Relative to a common origin
    public List<WallSegment> walls; // Details for each wall segment of the room
    public List<string> connectedRoomIds; // IDs of rooms directly accessible
    public string notes; // Any specific features mentioned in the blueprint
}

[System.Serializable]
public struct WallSegment
{
    public Vector3 start;
    public Vector3 end;
    public float height;
    public float thickness;
}

[System.Serializable]
public enum WallSide
{
    North,
    South,
    East,
    West
}

[System.Serializable]
public struct WallSegment
{
    /// <summary>
    /// Starting position of the wall relative to the room or global origin.
    /// </summary>
    public Vector3 startPoint;

    /// <summary>
    /// Ending position of the wall relative to the room or global origin.
    /// </summary>
    public Vector3 endPoint;

    /// <summary>
    /// Thickness of the wall in meters.
    /// </summary>
    public float thickness;

    /// <summary>
    /// Cardinal side of the room the wall sits on.
    /// </summary>
    public WallSide side;

    /// <summary>
    /// Whether this wall is part of the building exterior.
    /// </summary>
    public bool isExterior;

    /// <summary>
    /// IDs of doors contained within this wall.
    /// </summary>
    public List<string> doorIdsOnWall;

    /// <summary>
    /// IDs of windows contained within this wall.
    /// </summary>
    public List<string> windowIdsOnWall;

    /// <summary>
    /// IDs of openings such as passthroughs within this wall.
    /// </summary>
    public List<string> openingIdsOnWall;
}

[System.Serializable]
public struct DoorSpec
{
    // Placeholder for door specifications
}

[System.Serializable]
public struct WindowSpec
{
    // Placeholder for window specifications
}

[System.Serializable]
public enum OpeningType
{
    CasedOpening,
    PassthroughCounter
}

[System.Serializable]
public struct OpeningSpec
{
    /// <summary>
    /// Unique identifier for this opening.
    /// </summary>
    public string openingId;

    /// <summary>
    /// Type of the opening such as a simple cased opening or a passthrough counter.
    /// </summary>
    public OpeningType type;

    /// <summary>
    /// Width of the opening in meters.
    /// </summary>
    public float width;

    /// <summary>
    /// Height of the opening in meters.
    /// </summary>
    public float height;

    /// <summary>
    /// Position of the opening relative to the house origin. This is generally
    /// the center or bottom-center point.
    /// </summary>
    public Vector3 position;

    /// <summary>
    /// Identifier of the wall that this opening belongs to.
    /// </summary>
    public string wallId;

    /// <summary>
    /// Depth of the ledge for passthroughs. Zero if not applicable.
    /// </summary>
    public float passthroughLedgeDepth;

    /// <summary>
    /// ID of the first room that this opening connects to.
    /// </summary>
    public string connectsRoomA_Id;

    /// <summary>
    /// ID of the second room that this opening connects to.
    /// </summary>
    public string connectsRoomB_Id;
}
