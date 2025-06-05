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

    /// <summary>
    /// Calculates the overall bounding box that encompasses all rooms in the plan.
    /// </summary>
    /// <returns>Axis-aligned bounds covering the plan footprint.</returns>
    public Bounds CalculateBounds()
    {
        if (rooms == null || rooms.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;

        foreach (RoomData room in rooms)
        {
            if (room.dimensions.x <= 0f || room.dimensions.y <= 0f)
            {
                Debug.LogWarning($"Room {room.roomId} has non-positive dimensions: {room.dimensions}");
                continue;
            }

            Vector3 pos = room.position;
            Vector2 size = room.dimensions;
            minX = Mathf.Min(minX, pos.x);
            minZ = Mathf.Min(minZ, pos.z);
            maxX = Mathf.Max(maxX, pos.x + size.x);
            maxZ = Mathf.Max(maxZ, pos.z + size.y);
        }

        Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        Vector3 sizeVector = new Vector3(maxX - minX, 0f, maxZ - minZ);
        return new Bounds(center, sizeVector);
    }
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
    public Vector3 atticHatchLocalPosition; // ADDED: Local position for attic hatch, if any
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
public enum DoorType
{
    Hinged,
    Sliding,
    Pocket,
    BiFold,
    Overhead
}

[System.Serializable]
public enum SwingDirection
{
    InwardNorth,
    InwardSouth,
    InwardEast,
    InwardWest,
    OutwardNorth,
    OutwardSouth,
    OutwardEast,
    OutwardWest
}

[System.Serializable]
public enum SlideDirection
{
    SlidesLeft,
    SlidesRight
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
    /// <summary>
    /// Unique identifier for the door.
    /// </summary>
    public string doorId;

    /// <summary>
    /// The type of door such as hinged or sliding.
    /// </summary>
    public DoorType type;

    /// <summary>
    /// Width of the door in meters. Example: 0.81 for a 2'-8" door.
    /// </summary>
    public float width;

    /// <summary>
    /// Height of the door in meters. Defaults to around 2.03m (6'8").
    /// </summary>
    public float height;

    /// <summary>
    /// Position of the door relative to its wall or room.
    /// </summary>
    public Vector3 position;

    /// <summary>
    /// Identifier of the wall this door sits on.
    /// </summary>
    public string wallId;

    /// <summary>
    /// Swing direction details for hinged doors.
    /// </summary>
    public SwingDirection swingDirection;

    /// <summary>
    /// Slide direction details for sliding doors.
    /// </summary>
    public SlideDirection slideDirection;

    /// <summary>
    /// Whether this door is located on an exterior wall.
    /// </summary>
    public bool isExterior;

    /// <summary>
    /// ID of the first room connected by this door.
    /// </summary>
    public string connectsRoomA_Id;

    /// <summary>
    /// ID of the second room connected by this door.
    /// </summary>
    public string connectsRoomB_Id;
}

[System.Serializable]
public enum WindowType
{
    SingleHung,
    Sliding,
    Bay,
    FixedGlass,
    SkylightQuad
}

[System.Serializable]
public struct WindowSpec
{
    /// <summary>
    /// Unique identifier for the window.
    /// </summary>
    public string windowId;

    /// <summary>
    /// Type of window, e.g., single hung or bay.
    /// </summary>
    public WindowType type;

    /// <summary>
    /// Width of the window in meters.
    /// </summary>
    public float width;

    /// <summary>
    /// Height of the window in meters.
    /// </summary>
    public float height;

    /// <summary>
    /// Position of the window relative to its wall or room.
    /// </summary>
    public Vector3 position;

    /// <summary>
    /// Height from floor to the bottom of the window.
    /// </summary>
    public float sillHeight;

    /// <summary>
    /// Identifier of the wall this window is placed on.
    /// </summary>
    public string wallId;

    /// <summary>
    /// Whether the window can be opened.
    /// </summary>
    public bool isOperable;

    /// <summary>
    /// Number of panes for bay windows.
    /// </summary>
    public int bayPanes;

    /// <summary>
    /// Projection depth for bay windows in meters.
    /// </summary>
    public float bayProjectionDepth;
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
