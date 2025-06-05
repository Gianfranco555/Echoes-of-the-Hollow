using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents data about a detected opening in a wall.
/// </summary>
public struct OpeningData {
    /// <summary>
    /// Position of the opening's bottom-left corner in the local space of the wall root object.
    /// X is along the wall's length, Y is height from the wall's local origin.
    /// </summary>
    public Vector3 localPosition;
    public float width;
    public float height;
    public bool isDoorLike; // True if the opening reaches near the floor and is tall enough.
    public bool isWindowLike; // True if the opening is elevated from the floor and does not reach the ceiling.
}


/// <summary>
/// A static class responsible for analyzing wall GameObjects to derive structured WallSegment data.
/// </summary>
public static class WallSegmentAnalyzer {

    /// <summary>
    /// Holds the analyzed data for a wall segment, including its dimensions and openings.
    /// </summary>
    public struct AnalyzedWallData
    {
        /// <summary>
        /// The conceptual start point of the wall in its local space (always Vector3.zero).
        /// </summary>
        public Vector3 localStartPoint;

        /// <summary>
        /// The conceptual end point of the wall in its local space (wallLength, 0, 0).
        /// </summary>
        public Vector3 localEndPoint;

        public float wallLength;
        public List<OpeningData> openings;
        public bool isLikelyExterior; // Placeholder for future development.
        public float determinedThickness; // Placeholder, defaults to expectedWallThickness.
    }

    /// <summary>
    /// Helper class to store information about individual wall slices, including their bounds
    /// transformed into the coordinate space of the wallRootObject.
    /// </summary>
    private class WallSliceInfo {
        public float minX, maxX, minY, maxY, minZ, maxZ; // Extents in wallRootObject's local space
        public Bounds localBounds; // Bounds object in wallRootObject's local space
        public Transform transform; // Reference to the slice's transform

        /// <summary>
        /// Initializes WallSliceInfo by calculating the slice's bounds in the wallRootObject's local space.
        /// </summary>
        public WallSliceInfo(Transform sliceTransform, Transform wallRootTransform) {
            this.transform = sliceTransform;
            MeshFilter mf = sliceTransform.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) {
                // This case should ideally be filtered out before creating WallSliceInfo,
                // but initialize to safe values if it occurs.
                localBounds = new Bounds();
                minX = maxX = minY = maxY = minZ = maxZ = 0;
                return;
            }

            Bounds childMeshBounds = mf.sharedMesh.bounds; // Bounds in slice's own local space

            // To accurately get the extents in wallRootTransform's local space,
            // transform all 8 corners of the child's local bounds to world space,
            // then from world space to wallRootTransform's local space.
            Vector3[] corners = new Vector3[8];
            Vector3 center = childMeshBounds.center;
            Vector3 extents = childMeshBounds.extents;
            corners[0] = sliceTransform.TransformPoint(center + new Vector3(extents.x, extents.y, extents.z));
            corners[1] = sliceTransform.TransformPoint(center + new Vector3(extents.x, extents.y, -extents.z));
            corners[2] = sliceTransform.TransformPoint(center + new Vector3(extents.x, -extents.y, extents.z));
            corners[3] = sliceTransform.TransformPoint(center + new Vector3(extents.x, -extents.y, -extents.z));
            corners[4] = sliceTransform.TransformPoint(center + new Vector3(-extents.x, extents.y, extents.z));
            corners[5] = sliceTransform.TransformPoint(center + new Vector3(-extents.x, extents.y, -extents.z));
            corners[6] = sliceTransform.TransformPoint(center + new Vector3(-extents.x, -extents.y, extents.z));
            corners[7] = sliceTransform.TransformPoint(center + new Vector3(-extents.x, -extents.y, -extents.z));

            // Transform corners to wallRootObject's local space
            for (int i = 0; i < 8; i++) {
                corners[i] = wallRootTransform.InverseTransformPoint(corners[i]);
            }

            // Find min/max of the transformed corners to define the new AABB in wallRoot's space
            minX = corners.Min(v => v.x);
            maxX = corners.Max(v => v.x);
            minY = corners.Min(v => v.y);
            maxY = corners.Max(v => v.y);
            minZ = corners.Min(v => v.z);
            maxZ = corners.Max(v => v.z);

            // Create a Bounds object from these min/max values
            localBounds = new Bounds(); // Center and size will be set by SetMinMax
            localBounds.SetMinMax(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        }
    }

    /// <summary>
    /// Analyzes the geometry of a wall GameObject to determine its length, openings, and other properties.
    /// </summary>
    /// <param name="wallRootObject">The parent GameObject of the wall, defining its coordinate system.</param>
    /// <param name="roomFloorY">The world Y-coordinate of the room's floor.</param>
    /// <param name="storyHeight">The total height of the wall from floor to ceiling.</param>
    /// <param name="expectedWallThickness">The expected thickness of the wall, used for sampling.</param>
    /// <returns>An AnalyzedWallData struct containing the analysis results.</returns>
    public static AnalyzedWallData AnalyzeWallGeometry(GameObject wallRootObject, float roomFloorY, float storyHeight, float expectedWallThickness) {
        AnalyzedWallData data = new AnalyzedWallData();
        data.openings = new List<OpeningData>();
        data.wallLength = 0f;
        // Initialize placeholder values. These might be refined later.
        data.isLikelyExterior = false;
        data.determinedThickness = expectedWallThickness;

        if (wallRootObject == null) {
            Debug.LogError("WallSegmentAnalyzer: Wall root object is null.");
            return data; // Return data with defaults
        }

        // Transform the world-space roomFloorY to the wallRootObject's local Y coordinate.
        // This local Y value represents the base of the wall (sill level for openings starting at the floor).
        // Assumes wallRootObject's XZ plane is aligned with the wall's direction and thickness.
        Vector3 worldFloorAtWallOrigin = new Vector3(wallRootObject.transform.position.x, roomFloorY, wallRootObject.transform.position.z);
        float localWallBaseY = wallRootObject.transform.InverseTransformPoint(worldFloorAtWallOrigin).y;
        // The local Y coordinate of the top of the wall (ceiling level).
        float localWallTopY = localWallBaseY + storyHeight;

        // Collect all valid wall slices (children with MeshFilter and MeshRenderer)
        List<WallSliceInfo> wallSlices = new List<WallSliceInfo>();
        foreach (Transform childTransform in wallRootObject.transform) {
            MeshFilter mf = childTransform.GetComponent<MeshFilter>();
            // Ensure slice has both MeshFilter (with a mesh) and a MeshRenderer
            if (mf != null && mf.sharedMesh != null && childTransform.GetComponent<MeshRenderer>() != null) {
                wallSlices.Add(new WallSliceInfo(childTransform, wallRootObject.transform));
            }
        }

        if (wallSlices.Count == 0) {
            // No valid slices found, wall is considered to have zero length and no openings.
            data.localStartPoint = Vector3.zero;
            data.localEndPoint = Vector3.zero;
            // data.isLikelyExterior and data.determinedThickness already set to defaults
            return data;
        }

        // Determine overall wall length from the maximum X-extent of slices in wallRootObject's local space.
        // The wall is assumed to start at wallRootObject's local origin (0,0,0).
        float overallMaxX = wallSlices.Max(s => s.maxX); // Relies on WallSliceInfo.maxX

        data.wallLength = overallMaxX;
        data.localStartPoint = Vector3.zero; // By definition, wall starts at local origin
        data.localEndPoint = new Vector3(data.wallLength, 0, 0); // End point along local X axis

        // Sort slices by their minimum X position to process them along the wall's length.
        wallSlices = wallSlices.OrderBy(s => s.minX).ToList();

        List<OpeningData> detectedOpenings = new List<OpeningData>();
        float currentXTracker = 0f; // Tracks the X position along the wall as we find solid parts or gaps.

        // --- Pass 1: Detect full-height openings (horizontal gaps between slices) ---
        // Iterate through sorted slices to find gaps along the wall's length.
        for (int i = 0; i < wallSlices.Count; i++) {
            WallSliceInfo currentSlice = wallSlices[i];
            // If there's a significant space before the current slice starts, it's a full-height opening.
            if (currentSlice.minX > currentXTracker + 0.01f) { // Using a small tolerance (1cm)
                detectedOpenings.Add(new OpeningData {
                    localPosition = new Vector3(currentXTracker, localWallBaseY, 0), // Opening starts at floor level
                    width = currentSlice.minX - currentXTracker,
                    height = storyHeight, // Assumed to be full story height
                    // Classification (isDoorLike/isWindowLike) will be done later
                });
            }
            // Advance the tracker to the end of the current slice.
            currentXTracker = Mathf.Max(currentXTracker, currentSlice.maxX);
        }

        // After the last slice, check if there's a remaining gap up to the calculated wallLength.
        if (currentXTracker < data.wallLength - 0.01f) {
             detectedOpenings.Add(new OpeningData {
                localPosition = new Vector3(currentXTracker, localWallBaseY, 0),
                width = data.wallLength - currentXTracker,
                height = storyHeight,
            });
        }

        // --- Pass 2: Detect vertical openings (windows, partial doors) within segments that have slices ---
        // This pass samples the wall vertically at intervals along its length.
        // It looks for gaps between slices in the Y direction within areas covered by slices in X.
        float stepSize = expectedWallThickness > 0 ? expectedWallThickness * 0.5f : 0.1f; // Sampling step along X
        if (stepSize <= 0.001f) stepSize = 0.1f; // Ensure stepSize is positive and practical

        List<OpeningData> verticalOpeningsResult = new List<OpeningData>();
        for (float xSample = stepSize / 2f; xSample < data.wallLength; xSample += stepSize) {
            // Find all slices that overlap the current xSample point.
            List<WallSliceInfo> overlappingSlicesAtX = wallSlices
                .Where(s => xSample >= s.minX && xSample < s.maxX)
                .OrderBy(s => s.minY) // Sort them by their bottom Y position
                .ToList();

            // If no slices cover this xSample, it's part of a full-height opening (already found in Pass 1).
            if (!overlappingSlicesAtX.Any()) continue;

            float currentLocalYTracker = localWallBaseY; // Start checking from the floor level.
            foreach (var slice in overlappingSlicesAtX) {
                // If there's a gap between the currentYTracker and the bottom of this slice, it's an opening.
                if (slice.minY > currentLocalYTracker + 0.05f) { // 5cm tolerance for vertical gap
                    float openingHeight = slice.minY - currentLocalYTracker;
                    if (openingHeight > 0.1f) { // Minimum height for a meaningful opening (10cm)
                         verticalOpeningsResult.Add(new OpeningData {
                            // localPosition Y is the sill height of this detected vertical gap
                            localPosition = new Vector3(xSample - stepSize/2f, currentLocalYTracker, 0),
                            width = stepSize, // Initial width is the sample step; will be merged later
                            height = openingHeight,
                        });
                    }
                }
                // Advance the Y tracker to the top of the current slice.
                currentLocalYTracker = Mathf.Max(currentLocalYTracker, slice.maxY);
            }

            // After checking all slices at xSample, if currentLocalYTracker is below wall top, there's a gap above the topmost slice.
            if (localWallTopY > currentLocalYTracker + 0.05f) {
                float openingHeight = localWallTopY - currentLocalYTracker;
                if (openingHeight > 0.1f) {
                    verticalOpeningsResult.Add(new OpeningData {
                        localPosition = new Vector3(xSample - stepSize/2f, currentLocalYTracker, 0),
                        width = stepSize,
                        height = openingHeight,
                    });
                }
            }
        }

        // Merge fragmented vertical openings and add them to the main list.
        detectedOpenings.AddRange(MergeOpenings(verticalOpeningsResult));
        // Classify all detected openings (both full-height and vertical)
        data.openings = ClassifyOpenings(detectedOpenings, localWallBaseY, storyHeight);

        // isLikelyExterior and determinedThickness retain their default/input values for now.
        return data;
    }

    /// <summary>
    /// Merges small, fragmented openings (typically from vertical sampling) into larger, coherent openings.
    /// </summary>
    private static List<OpeningData> MergeOpenings(List<OpeningData> openings) {
        if (openings.Count < 2) return openings; // No merging needed for 0 or 1 opening

        // Sort by X, then Y to make merging easier
        var sortedOpenings = openings.OrderBy(o => o.localPosition.x).ThenBy(o => o.localPosition.y).ToList();
        List<OpeningData> merged = new List<OpeningData>();
        if (!sortedOpenings.Any()) return merged;

        OpeningData currentMergeCandidate = sortedOpenings[0];

        for (int i = 1; i < sortedOpenings.Count; i++) {
            OpeningData nextOpening = sortedOpenings[i];

            // Condition for horizontal merge: X-contiguity and similar Y-position and height.
            bool xContiguous = Mathf.Abs((currentMergeCandidate.localPosition.x + currentMergeCandidate.width) - nextOpening.localPosition.x) < 0.1f; // Within 10cm
            bool yAligned = Mathf.Abs(currentMergeCandidate.localPosition.y - nextOpening.localPosition.y) < 0.1f && // Sill heights similar
                            Mathf.Abs(currentMergeCandidate.height - nextOpening.height) < 0.1f; // Heights similar

            if (xContiguous && yAligned) { // Merge horizontally
                float newX = Mathf.Min(currentMergeCandidate.localPosition.x, nextOpening.localPosition.x);
                currentMergeCandidate.width = (nextOpening.localPosition.x + nextOpening.width) - newX; // Extend width
                currentMergeCandidate.localPosition = new Vector3(newX, currentMergeCandidate.localPosition.y, 0);
            }
            // Condition for vertical merge: Y-contiguity and similar X-position and width.
            else {
                 bool yContiguous = Mathf.Abs((currentMergeCandidate.localPosition.y + currentMergeCandidate.height) - nextOpening.localPosition.y) < 0.1f; // Vertically touching
                 bool xAligned = Mathf.Abs(currentMergeCandidate.localPosition.x - nextOpening.localPosition.x) < 0.1f && // X positions similar
                                Mathf.Abs(currentMergeCandidate.width - nextOpening.width) < 0.1f; // Widths similar

                if(yContiguous && xAligned) { // Merge vertically
                    float newY = Mathf.Min(currentMergeCandidate.localPosition.y, nextOpening.localPosition.y);
                    currentMergeCandidate.height = (nextOpening.localPosition.y + nextOpening.height) - newY; // Extend height
                    currentMergeCandidate.localPosition = new Vector3(currentMergeCandidate.localPosition.x, newY, 0); // Keep X, update Y
                } else {
                    // No merge possible, add current candidate to results and start new candidate
                    merged.Add(currentMergeCandidate);
                    currentMergeCandidate = nextOpening;
                }
            }
        }
        merged.Add(currentMergeCandidate); // Add the last processed/merged opening

        // Filter out any tiny openings that might remain after merging or were too small to begin with.
        merged.RemoveAll(o => o.width < 0.05f || o.height < 0.05f); // Min 5cm width/height
        return merged;
    }

    /// <summary>
    /// Classifies a list of detected openings into doors or windows based on their dimensions and position.
    /// </summary>
    private static List<OpeningData> ClassifyOpenings(List<OpeningData> openings, float localWallBaseY, float storyHeight) {
        List<OpeningData> classifiedOpenings = new List<OpeningData>();
        if (openings == null) return classifiedOpenings;

        // Define thresholds for classification (these can be tuned)
        const float doorMinHeight = 1.8f;       // Minimum height for a door
        const float doorMaxSillHeight = 0.15f;  // Maximum sill height from floor for a door
        const float windowMinSillHeight = 0.3f; // Minimum sill height from floor for a window
        const float windowMinHeight = 0.2f;     // Minimum height for a window
        const float windowMaxHeaderProximityToCeiling = 0.2f; // Min distance from window top to ceiling

        float localWallTopY = localWallBaseY + storyHeight; // Y-coordinate of the ceiling in local space

        foreach (var opening in openings) {
            OpeningData classified = opening; // Work on a copy

            // Calculate sill height relative to the actual base of the wall (localWallBaseY)
            float sillHeightFromFloor = classified.localPosition.y - localWallBaseY;
            // Calculate the Y-coordinate of the top of the opening in local space
            float openingTopYRelativeToWallOrigin = classified.localPosition.y + classified.height;

            // Default to false
            classified.isDoorLike = false;
            classified.isWindowLike = false;

            // Door-like conditions:
            // 1. Sill is very close to the floor.
            // 2. Height is substantial (typical door height).
            if (sillHeightFromFloor <= doorMaxSillHeight && classified.height >= doorMinHeight) {
                classified.isDoorLike = true;
            }

            // Window-like conditions:
            // 1. Sill is elevated from the floor.
            // 2. Top of the window is clearly below the ceiling.
            // 3. Height is reasonable for a window.
            if (sillHeightFromFloor >= windowMinSillHeight &&
                (openingTopYRelativeToWallOrigin < localWallTopY - windowMaxHeaderProximityToCeiling) &&
                classified.height >= windowMinHeight) {
                classified.isWindowLike = true;
            }

            // Ensure mutual exclusivity: if it's a door, it's not a window.
            if (classified.isDoorLike) {
                classified.isWindowLike = false;
            }

            // Add to results if it's classified as either, or if it's a significant unclassified opening
            // Pass 1 openings (full height) are often doors or large passages.
            bool isFullHeightOpening = Mathf.Abs(classified.height - storyHeight) < 0.1f && sillHeightFromFloor <= doorMaxSillHeight;
            if(isFullHeightOpening && !classified.isDoorLike && !classified.isWindowLike) {
                // If it's a full-height opening starting at the floor, and not already classified as a window,
                // classify it as a door.
                classified.isDoorLike = true;
                classified.isWindowLike = false;
            }

            // Only add openings that have a meaningful size AND have been classified as something OR are significant full-height openings.
            if ((classified.isDoorLike || classified.isWindowLike) && classified.width > 0.1f && classified.height > 0.1f) {
                 classifiedOpenings.Add(classified);
            } else if (isFullHeightOpening && classified.width > 0.1f) { // Catchall for large full-height openings if not previously added
                 classified.isDoorLike = true; // Default full height unclassified to door
                 classified.isWindowLike = false;
                 // Avoid adding duplicates if already added by the first condition
                 if (!classifiedOpenings.Any(o =>
                    Mathf.Approximately(o.localPosition.x, classified.localPosition.x) &&
                    Mathf.Approximately(o.localPosition.y, classified.localPosition.y) &&
                    Mathf.Approximately(o.width, classified.width) &&
                    Mathf.Approximately(o.height, classified.height))) {
                    classifiedOpenings.Add(classified);
                 }
            }
        }
        return classifiedOpenings;
    }
}
