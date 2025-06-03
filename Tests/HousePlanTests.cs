using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unit tests verifying the integrity of data loaded into <see cref="HousePlanSO"/> assets.
/// </summary>
public class HousePlanTests
{
    private HousePlanSO plan;

    /// <summary>
    /// Locates the first <see cref="HousePlanSO"/> asset in the project for testing.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        string[] guids = AssetDatabase.FindAssets("t:HousePlanSO");
        Assert.IsNotEmpty(guids, "No HousePlanSO assets found. Run BlueprintImporter to create one.");
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        plan = AssetDatabase.LoadAssetAtPath<HousePlanSO>(path);
        Assert.IsNotNull(plan, $"Failed to load HousePlanSO from {path}");
    }

    /// <summary>
    /// Validates the Foyer information imported from section 2 of the blueprint.
    /// </summary>
    [Test]
    public void TestFoyerData()
    {
        Assert.IsNotNull(plan);

        RoomData foyer = plan.rooms.Find(r => r.roomId == "Foyer");
        Assert.IsNotNull(foyer, "Foyer room not found in plan.");

        // Verify approximate dimensions (6ft x 6ft -> 1.8288m)
        Assert.AreEqual(1.8288f, foyer.dimensions.x, 0.01f);
        Assert.AreEqual(1.8288f, foyer.dimensions.y, 0.01f);

        // Count doors and openings associated with the foyer
        int doorCount = plan.doors.FindAll(
            d => d.connectsRoomA_Id == "Foyer" || d.connectsRoomB_Id == "Foyer").Count;
        Assert.AreEqual(2, doorCount, "Unexpected number of foyer doors.");

        int openingCount = plan.openings.FindAll(
            o => o.connectsRoomA_Id == "Foyer" || o.connectsRoomB_Id == "Foyer").Count;
        Assert.AreEqual(1, openingCount, "Unexpected number of foyer openings.");

        DoorSpec frontDoor = plan.doors.Find(d => d.doorId == "FrontDoor");
        Assert.IsNotNull(frontDoor, "Front Door not found in plan.");
        Assert.AreEqual(0.9144f, frontDoor.width, 0.01f);
        Assert.AreEqual(SwingDirection.InwardSouth, frontDoor.swingDirection);
    }
}
