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
public struct OpeningSpec
{
    // Placeholder for passthrough openings
}
