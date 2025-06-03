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
    // Placeholder for room details
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
public struct OpeningSpec
{
    // Placeholder for passthrough openings
}
