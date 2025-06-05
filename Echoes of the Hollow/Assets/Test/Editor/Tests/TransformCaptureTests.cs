using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection; // Added for reflection

// Assuming TransformCaptureWindow and its enums are in the global namespace or an accessible one.
// For this test file, we are directly calling TransformCaptureWindow.DetectComponentType,
// which relies on TransformCaptureWindow.HouseComponentType.
// Ensure TransformCaptureWindow.cs is compiled and accessible.
// If TransformCaptureWindow is in a specific namespace, e.g., "HouseTools",
// a 'using HouseTools;' statement would be needed here.

public class TransformCaptureTests
{
    private List<GameObject> gameObjectsToCleanup = new List<GameObject>();
    private TransformCaptureWindow captureWindowInstance; // Added for instance method testing

    // Helper method to create a basic GameObject for testing
    private GameObject CreateTestGameObject(string name, Vector3 position = default, Vector3 scale = default, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position == default ? Vector3.zero : position;
        go.transform.localScale = scale == default ? Vector3.one : scale;
        if (parent != null)
        {
            go.transform.SetParent(parent);
        }

        if (go.GetComponent<MeshFilter>() == null) go.AddComponent<MeshFilter>();
        if (go.GetComponent<MeshRenderer>() == null) go.AddComponent<MeshRenderer>();

        gameObjectsToCleanup.Add(go);
        return go;
    }

    [SetUp]
    public void TestSetup()
    {
        // Create an instance of the window for testing its instance methods
        captureWindowInstance = ScriptableObject.CreateInstance<TransformCaptureWindow>();
        // Ensure gameObjectsToCleanup is cleared for each test (it's also cleared in TearDown but good practice here too)
        gameObjectsToCleanup.Clear();
    }

    [TearDown]
    public void TearDown() // Combined TearDown logic
    {
        if (captureWindowInstance != null)
        {
            Object.DestroyImmediate(captureWindowInstance);
            captureWindowInstance = null;
        }

        foreach (var obj in gameObjectsToCleanup)
        {
            if (obj != null) Object.DestroyImmediate(obj);
        }
        gameObjectsToCleanup.Clear();
    }

    // Helper to set mesh bounds for a GameObject
    private void SetObjectBounds(GameObject obj, Vector3 size)
    {
        MeshFilter mf = obj.GetComponent<MeshFilter>();
        if (mf == null) mf = obj.AddComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        // Create a simple cube mesh for bounds purposes
        Vector3 halfSize = size / 2f;
        mesh.vertices = new Vector3[] {
            new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
            new Vector3(halfSize.x, halfSize.y, -halfSize.z), new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
            new Vector3(-halfSize.x, -halfSize.y, halfSize.z), new Vector3(halfSize.x, -halfSize.y, halfSize.z),
            new Vector3(halfSize.x, halfSize.y, halfSize.z), new Vector3(-halfSize.x, halfSize.y, halfSize.z)
        };
        mesh.triangles = new int[] { // Simplified cube triangles
            0,2,1, 0,3,2, 1,2,6, 1,6,5, 0,1,5, 0,5,4, 3,7,6, 3,6,2, 4,5,6, 4,6,7, 0,4,7, 0,7,3
        };
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        if (obj.GetComponent<MeshRenderer>() == null) obj.AddComponent<MeshRenderer>();
    }


    [Test]
    public void DetectComponentType_Room_ByName()
    {
        GameObject obj = CreateTestGameObject("Room_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Room, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Room_ByComponent()
    {
        GameObject obj = CreateTestGameObject("MyLivingArea_TestObj");
        obj.AddComponent<RoomIdentifier>();
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Room, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Wall()
    {
        GameObject obj = CreateTestGameObject("Wall_Main_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Wall, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Door()
    {
        GameObject obj = CreateTestGameObject("Door_Entry_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Door, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Window()
    {
        GameObject obj = CreateTestGameObject("Window_Kitchen_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Window, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Foundation()
    {
        GameObject obj = CreateTestGameObject("Foundation_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Foundation, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Roof()
    {
        GameObject obj = CreateTestGameObject("Roof_Tiles_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Roof, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_ProceduralHouseRoot()
    {
        GameObject obj = CreateTestGameObject("ProceduralHouse_Generated_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.ProceduralHouseRoot, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Unknown()
    {
        GameObject obj = CreateTestGameObject("GenericObject123_TestObj");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Unknown, TransformCaptureWindow.DetectComponentType(obj));
    }

    [Test]
    public void DetectComponentType_Hierarchy_IdentifiesCorrectObject()
    {
        GameObject parentObj = CreateTestGameObject("Wall_Parent_TestObj");
        GameObject childObj = CreateTestGameObject("Door_Child_TestObj", parent: parentObj.transform);

        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Wall, TransformCaptureWindow.DetectComponentType(parentObj), "Parent object detection failed.");
        Assert.AreEqual(TransformCaptureWindow.HouseComponentType.Door, TransformCaptureWindow.DetectComponentType(childObj), "Child object detection failed.");
    }


    [Test]
    public void ConvertToRoomRelative_SimpleConversion()
    {
        Vector3 worldPosition = new Vector3(10f, 5f, 3f);
        Vector3 roomWorldOrigin = new Vector3(8f, 2f, 1f);
        Vector3 expectedRelativePosition = new Vector3(2f, 3f, 2f);

        Vector3 actualRelativePosition = TransformCaptureWindow.ConvertToRoomRelative(worldPosition, roomWorldOrigin);

        Assert.AreEqual(expectedRelativePosition.x, actualRelativePosition.x, 0.001f, "X-coordinate mismatch.");
        Assert.AreEqual(expectedRelativePosition.y, actualRelativePosition.y, 0.001f, "Y-coordinate mismatch.");
        Assert.AreEqual(expectedRelativePosition.z, actualRelativePosition.z, 0.001f, "Z-coordinate mismatch.");
    }

    [Test]
    public void ConvertToRoomRelative_ZeroOrigin()
    {
        Vector3 worldPosition = new Vector3(10f, 5f, 3f);
        Vector3 roomWorldOrigin = Vector3.zero;
        Vector3 expectedRelativePosition = worldPosition;

        Vector3 actualRelativePosition = TransformCaptureWindow.ConvertToRoomRelative(worldPosition, roomWorldOrigin);

        Assert.AreEqual(expectedRelativePosition.x, actualRelativePosition.x, 0.001f);
        Assert.AreEqual(expectedRelativePosition.y, actualRelativePosition.y, 0.001f);
        Assert.AreEqual(expectedRelativePosition.z, actualRelativePosition.z, 0.001f);
    }

    [Test]
    public void ConvertToWallRelative_NoRotation()
    {
        GameObject wallRoot = CreateTestGameObject("TestWall_NoRotation", new Vector3(5f, 0f, 5f));
        Vector3 worldPoint = new Vector3(7f, 1f, 6f);
        Vector3 expectedWallRelativePoint = new Vector3(2f, 1f, 1f);

        Vector3 actualWallRelativePoint = TransformCaptureWindow.ConvertToWallRelative(worldPoint, wallRoot.transform);

        Assert.AreEqual(expectedWallRelativePoint.x, actualWallRelativePoint.x, 0.001f, "X-coordinate mismatch.");
        Assert.AreEqual(expectedWallRelativePoint.y, actualWallRelativePoint.y, 0.001f, "Y-coordinate mismatch.");
        Assert.AreEqual(expectedWallRelativePoint.z, actualWallRelativePoint.z, 0.001f, "Z-coordinate mismatch.");
    }

    [Test]
    public void ConvertToWallRelative_WithRotation()
    {
        GameObject wallRoot = CreateTestGameObject("TestWall_WithRotation", new Vector3(5f, 0f, 5f));
        wallRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        Vector3 worldPoint = new Vector3(5f, 1f, 7f);
        Vector3 expectedWallRelativePoint = new Vector3(-2f, 1f, 0f);

        Vector3 actualWallRelativePoint = TransformCaptureWindow.ConvertToWallRelative(worldPoint, wallRoot.transform);

        Assert.AreEqual(expectedWallRelativePoint.x, actualWallRelativePoint.x, 0.001f, "X-coordinate mismatch.");
        Assert.AreEqual(expectedWallRelativePoint.y, actualWallRelativePoint.y, 0.001f, "Y-coordinate mismatch.");
        Assert.AreEqual(expectedWallRelativePoint.z, actualWallRelativePoint.z, 0.001f, "Z-coordinate mismatch.");
    }

    [Test]
    public void ConvertToWallRelative_WithParentAndChildRotation()
    {
        GameObject grandparent = CreateTestGameObject("Grandparent", new Vector3(1f, 1f, 1f));
        grandparent.transform.rotation = Quaternion.Euler(0, 30f, 0);

        GameObject wallRoot = CreateTestGameObject("TestWall_ChildRotation", Vector3.zero);
        wallRoot.transform.SetParent(grandparent.transform, false);
        wallRoot.transform.localRotation = Quaternion.Euler(0, 60f, 0);

        Vector3 worldPoint = new Vector3(1f, 2f, 3f);
        Vector3 expectedWallRelativePoint = new Vector3(-2f, 1f, 0f);

        Vector3 actualWallRelativePoint = TransformCaptureWindow.ConvertToWallRelative(worldPoint, wallRoot.transform);

        Assert.AreEqual(expectedWallRelativePoint.x, actualWallRelativePoint.x, 0.001f, "X-coordinate mismatch.");
        Assert.AreEqual(expectedWallRelativePoint.y, actualWallRelativePoint.y, 0.001f, "Y-coordinate mismatch.");
        Assert.AreEqual(expectedWallRelativePoint.z, actualWallRelativePoint.z, 0.001f, "Z-coordinate mismatch.");
    }

    private GameObject CreateWallSlice(string name, Transform parent, Vector3 localPosition, Vector3 size)
    {
        GameObject slice = new GameObject(name);
        slice.transform.SetParent(parent);
        slice.transform.localPosition = localPosition;
        slice.transform.localScale = Vector3.one;

        MeshFilter mf = slice.AddComponent<MeshFilter>();
        MeshRenderer mr = slice.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(-size.x / 2f, -size.y / 2f, 0),
            new Vector3( size.x / 2f, -size.y / 2f, 0),
            new Vector3(-size.x / 2f,  size.y / 2f, 0),
            new Vector3( size.x / 2f,  size.y / 2f, 0)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;

        if (!gameObjectsToCleanup.Contains(slice))
        {
            gameObjectsToCleanup.Add(slice);
        }
        return slice;
    }

    [Test]
    public void AnalyzeWallGeometry_SimpleSolidWall()
    {
        GameObject wallRoot = CreateTestGameObject("Wall_SolidRoot");
        CreateWallSlice("Slice1", wallRoot.transform, new Vector3(2.5f, 1.35f, 0f), new Vector3(5f, 2.7f, 0.1f));

        float roomFloorY = 0f;
        float storyHeight = 2.7f;
        float wallThickness = 0.1f;

        WallSegmentAnalyzer.AnalyzedWallData analyzedData = WallSegmentAnalyzer.AnalyzeWallGeometry(wallRoot, roomFloorY, storyHeight, wallThickness);

        Assert.AreEqual(5.0f, analyzedData.wallLength, 0.01f, "Wall length mismatch.");
        Assert.AreEqual(0, analyzedData.openings.Count, "Should be no openings.");
        Assert.AreEqual(new Vector3(5.0f, 0, 0), analyzedData.localEndPoint, "Local end point mismatch.");
        Assert.AreEqual(wallThickness, analyzedData.determinedThickness, 0.001f, "Determined thickness should match input for simple wall.");
    }

    [Test]
    public void AnalyzeWallGeometry_EmptyWallRoot()
    {
        GameObject wallRoot = CreateTestGameObject("Wall_EmptyRoot");
        WallSegmentAnalyzer.AnalyzedWallData analyzedData = WallSegmentAnalyzer.AnalyzeWallGeometry(wallRoot, 0f, 2.7f, 0.1f);

        Assert.AreEqual(0f, analyzedData.wallLength, 0.01f, "Wall length should be 0 for empty root.");
        Assert.AreEqual(0, analyzedData.openings.Count, "Should be no openings for empty root.");
        Assert.AreEqual(Vector3.zero, analyzedData.localEndPoint, "Local end point should be zero for empty root.");
    }

    [Test]
    public void AnalyzeWallGeometry_WallWithDoorGap()
    {
        GameObject wallRoot = CreateTestGameObject("Wall_DoorGapRoot");
        float storyHeight = 2.7f;
        float wallThickness = 0.1f;
        float roomFloorY = wallRoot.transform.position.y;

        CreateWallSlice("SliceLeft", wallRoot.transform, new Vector3(0.5f, storyHeight / 2f, 0f), new Vector3(1f, storyHeight, wallThickness));
        CreateWallSlice("SliceRight", wallRoot.transform, new Vector3(2.5f, storyHeight / 2f, 0f), new Vector3(1f, storyHeight, wallThickness));

        WallSegmentAnalyzer.AnalyzedWallData analyzedData = WallSegmentAnalyzer.AnalyzeWallGeometry(wallRoot, roomFloorY, storyHeight, wallThickness);

        Assert.AreEqual(3.0f, analyzedData.wallLength, 0.01f, "Wall length mismatch.");
        Assert.AreEqual(1, analyzedData.openings.Count, "Should be one door-like opening.");

        if (analyzedData.openings.Count == 1)
        {
            var opening = analyzedData.openings[0];
            Assert.IsTrue(opening.isDoorLike, "Opening should be classified as door-like.");
            Assert.IsFalse(opening.isWindowLike, "Opening should not be classified as window-like.");
            Assert.AreEqual(1.0f, opening.localPosition.x, 0.01f, "Opening X position incorrect.");
            Assert.AreEqual(0f, opening.localPosition.y, 0.01f, "Opening Y position (sill) incorrect.");
            Assert.AreEqual(1.0f, opening.width, 0.01f, "Opening width incorrect.");
            Assert.AreEqual(storyHeight, opening.height, 0.01f, "Opening height incorrect for full gap.");
        }
    }

    [Test]
    public void AnalyzeWallGeometry_WallWithWindowOpening()
    {
        GameObject wallRoot = CreateTestGameObject("Wall_WindowOpeningRoot");
        float storyHeight = 2.7f;
        float wallThickness = 0.1f;
        float roomFloorY = wallRoot.transform.position.y;

        float windowSillHeight = 0.9f;
        float windowHeight = 1.2f;
        float wallSegmentLength = 2.0f;

        CreateWallSlice("SillSlice", wallRoot.transform, new Vector3(wallSegmentLength/2f, windowSillHeight/2f, 0f), new Vector3(wallSegmentLength, windowSillHeight, wallThickness));
        float headerBottomY = windowSillHeight + windowHeight;
        float headerHeight = storyHeight - headerBottomY;
        CreateWallSlice("HeaderSlice", wallRoot.transform, new Vector3(wallSegmentLength/2f, headerBottomY + headerHeight/2f, 0f), new Vector3(wallSegmentLength, headerHeight, wallThickness));

        WallSegmentAnalyzer.AnalyzedWallData analyzedData = WallSegmentAnalyzer.AnalyzeWallGeometry(wallRoot, roomFloorY, storyHeight, wallThickness);

        Assert.AreEqual(wallSegmentLength, analyzedData.wallLength, 0.01f, "Wall length mismatch.");
        Assert.AreEqual(1, analyzedData.openings.Count, "Should be one window-like opening.");

        if (analyzedData.openings.Count == 1)
        {
            var opening = analyzedData.openings[0];
            Assert.IsTrue(opening.isWindowLike, "Opening should be classified as window-like.");
            Assert.IsFalse(opening.isDoorLike, "Opening should not be classified as door-like.");
            Assert.AreEqual(0f, opening.localPosition.x, 0.1f, "Opening X position incorrect.");
            Assert.AreEqual(windowSillHeight, opening.localPosition.y, 0.01f, "Opening Y position (sill) incorrect.");
            Assert.AreEqual(wallSegmentLength, opening.width, 0.1f, "Opening width incorrect.");
            Assert.AreEqual(windowHeight, opening.height, 0.01f, "Opening height incorrect.");
        }
    }

    [Test]
    public void AnalyzeWallGeometry_WallWithMultipleOpenings()
    {
        GameObject wallRoot = CreateTestGameObject("Wall_MultiOpeningRoot");
        float storyHeight = 2.7f;
        float wallThickness = 0.1f;
        float roomFloorY = wallRoot.transform.position.y;

        CreateWallSlice("SolidLeft", wallRoot.transform, new Vector3(0.5f, storyHeight / 2f, 0f), new Vector3(1f, storyHeight, wallThickness));

        float windowSillY = 0.9f;
        float windowHeight = 1.2f;
        CreateWallSlice("MiddleSill", wallRoot.transform, new Vector3(3f, windowSillY / 2f, 0f), new Vector3(2f, windowSillY, wallThickness));
        float headerBottomY = windowSillY + windowHeight;
        float headerHeight = storyHeight - headerBottomY;
        CreateWallSlice("MiddleHeader", wallRoot.transform, new Vector3(3f, headerBottomY + headerHeight / 2f, 0f), new Vector3(2f, headerHeight, wallThickness));

        CreateWallSlice("SolidRight", wallRoot.transform, new Vector3(4.5f, storyHeight / 2f, 0f), new Vector3(1f, storyHeight, wallThickness));

        WallSegmentAnalyzer.AnalyzedWallData analyzedData = WallSegmentAnalyzer.AnalyzeWallGeometry(wallRoot, roomFloorY, storyHeight, wallThickness);

        Assert.AreEqual(5.0f, analyzedData.wallLength, 0.01f, "Wall length mismatch.");
        Assert.AreEqual(2, analyzedData.openings.Count, "Should be two openings (door and window).");

        if (analyzedData.openings.Count == 2)
        {
            var sortedOpenings = analyzedData.openings.OrderBy(o => o.localPosition.x).ToList();
            var doorOpening = sortedOpenings.FirstOrDefault(o => o.isDoorLike);
            var windowOpening = sortedOpenings.FirstOrDefault(o => o.isWindowLike);

            // A better check for a struct is to see if it has the property we searched for.
            Assert.IsTrue(doorOpening.isDoorLike, "Door-like opening not found or not classified correctly.");
            Assert.IsTrue(windowOpening.isWindowLike, "Window-like opening not found or not classified correctly.");

            if (doorOpening.isDoorLike)
            {
                Assert.AreEqual(1.0f, doorOpening.localPosition.x, 0.01f, "Door X pos incorrect.");
                Assert.AreEqual(0f, doorOpening.localPosition.y, 0.01f, "Door Y pos incorrect.");
                Assert.AreEqual(1.0f, doorOpening.width, 0.01f, "Door width incorrect.");
                Assert.AreEqual(storyHeight, doorOpening.height, 0.01f, "Door height incorrect.");
            }

            if (windowOpening.isWindowLike)
            {
                Assert.AreEqual(2.0f, windowOpening.localPosition.x, 0.1f, "Window X pos incorrect.");
                Assert.AreEqual(windowSillY, windowOpening.localPosition.y, 0.01f, "Window Y pos incorrect.");
                Assert.AreEqual(2.0f, windowOpening.width, 0.1f, "Window width incorrect.");
                Assert.AreEqual(windowHeight, windowOpening.height, 0.01f, "Window height incorrect.");
            }
        }
    }

    [Test]
    public void FormatAsRoomData_GeneratesCorrectString()
    {
        GameObject roomObj = CreateTestGameObject("TestRoom1");
        roomObj.transform.position = new Vector3(1f, 0f, 3f);
        SetObjectBounds(roomObj, new Vector3(4f, 2.7f, 5f));

        string result = captureWindowInstance.FormatAsRoomData(roomObj);

        StringAssert.Contains("roomId = \"TestRoom1\"", result);
        StringAssert.Contains("dimensions = new Vector2(4.000f, 5.000f)", result);
        StringAssert.Contains("position = new Vector3(1.000f, 0.000f, 3.000f)", result);
    }

    [Test]
    public void FormatAsWallSegment_GeneratesCorrectString()
    {
        GameObject wallRoot = CreateTestGameObject("TestWallSeg_01");
        wallRoot.transform.position = new Vector3(10f, 0f, 10f);
        CreateWallSlice("Slice_WS", wallRoot.transform, new Vector3(1.5f, 1.35f, 0f), new Vector3(3f, 2.7f, 0.1f));

        float roomFloorY = 0f;
        float storyHeight = 2.7f;
        float wallThickness = 0.15f;

        string result = captureWindowInstance.FormatAsWallSegment(wallRoot, roomFloorY, storyHeight, wallThickness);

        StringAssert.Contains($"// WallSegment for \"{wallRoot.name}\"", result);
        StringAssert.Contains("startPoint = new Vector3(10.000f, 0.000f, 10.000f)", result);
        StringAssert.Contains("endPoint = new Vector3(13.000f, 0.000f, 10.000f)", result);
        StringAssert.Contains($"thickness = {wallThickness.ToString("F3")}", result);
        StringAssert.Contains("isExterior = false", result);
    }

    [Test]
    public void FormatAsDoorSpec_WorldSpace_GeneratesCorrectString()
    {
        GameObject doorObj = CreateTestGameObject("TestDoor_01");
        doorObj.transform.position = new Vector3(2f, 0f, 5f);
        SetObjectBounds(doorObj, new Vector3(0.8f, 2.0f, 0.1f));

        string result = captureWindowInstance.FormatAsDoorSpec(doorObj);

        StringAssert.Contains("doorId = \"TestDoor_01\"", result);
        StringAssert.Contains("width = 0.800f", result);
        StringAssert.Contains("height = 2.000f", result);
        StringAssert.Contains("position = new Vector3(2.000f, 0.000f, 5.000f); // Position (World Space)", result);
        StringAssert.Contains("type = DoorType.Hinged", result);
    }

    [Test]
    public void FormatAsWindowSpec_RoomRelative_GeneratesCorrectString()
    {
        GameObject roomObj = CreateTestGameObject("TestRoomForWin");
        roomObj.transform.position = new Vector3(10f, 1f, 10f);

        GameObject windowObj = CreateTestGameObject("TestWindow_01", parent: roomObj.transform);
        windowObj.transform.localPosition = new Vector3(1f, 0.5f, 0.2f);
        SetObjectBounds(windowObj, new Vector3(1.2f, 1.0f, 0.1f));

        float roomFloorY = 1f;

        FieldInfo field = typeof(TransformCaptureWindow).GetField("selectedCoordinateSpace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(captureWindowInstance, TransformCaptureWindow.CoordinateSpaceSetting.RoomRelative);
        } else {
            Assert.Fail("Could not find or set selectedCoordinateSpace field for testing.");
        }

        string result = captureWindowInstance.FormatAsWindowSpec(windowObj, roomFloorY);

        StringAssert.Contains("windowId = \"TestWindow_01\"", result);
        StringAssert.Contains("width = 1.200f", result);
        StringAssert.Contains("height = 1.000f", result);
        StringAssert.Contains("position = new Vector3(1.000f, 0.500f, 0.200f); // Window Position (Room Relative to 'TestRoomForWin')", result);
        StringAssert.Contains("sillHeight = 0.500f", result);
        StringAssert.Contains("type = global::WindowType.SingleHung", result);
    }
}
