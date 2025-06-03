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
