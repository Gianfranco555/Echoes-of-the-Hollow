using UnityEngine;

/// <summary>
/// Utility for instantiating placeholder window prefabs based on a
/// <see cref="HousePlanSO"/> definition.
/// </summary>
public static class WindowPlacer
{
    /// <summary>
    /// Instantiates placeholder window prefabs for all <see cref="WindowSpec"/>
    /// entries in the supplied plan.
    /// </summary>
    /// <param name="housePlan">Plan containing window specs.</param>
    /// <param name="wallsContainer">Parent transform containing generated wall segments.</param>
    public static void PlaceAllWindows(HousePlanSO housePlan, GameObject wallsContainer)
    {
        if (housePlan == null || wallsContainer == null)
        {
            Debug.LogError("Invalid arguments supplied to WindowPlacer.PlaceAllWindows");
            return;
        }

        foreach (WindowSpec spec in housePlan.windows)
        {
            GameObject prefab = LoadWindowPrefab(spec.type);
            if (prefab == null)
            {
                Debug.LogWarning($"No prefab found for window type {spec.type}");
                continue;
            }

            Transform wall = FindWallTransform(spec.wallId, wallsContainer.transform);
            if (wall == null)
            {
                Debug.LogWarning($"Wall {spec.wallId} not found for window {spec.windowId}");
                continue;
            }

            GameObject instance = Object.Instantiate(prefab, wall);
            instance.name = $"Window_{spec.windowId}";
            instance.transform.localPosition = new Vector3(spec.position.x, spec.sillHeight, spec.position.z);
            instance.transform.localRotation = Quaternion.identity;
        }
    }

    private static GameObject LoadWindowPrefab(WindowType type)
    {
        string prefabName = type switch
        {
            WindowType.SingleHung => "Window_SingleHung_Placeholder",
            WindowType.Sliding => "Window_Sliding_Placeholder",
            WindowType.Bay => "Window_Bay_Placeholder",
            WindowType.SkylightQuad => "Skylight_Quad_Placeholder",
            _ => null
        };

        if (string.IsNullOrEmpty(prefabName))
        {
            return null;
        }

        return Resources.Load<GameObject>(prefabName);
    }

    private static Transform FindWallTransform(string wallId, Transform container)
    {
        foreach (Transform child in container)
        {
            if (child.gameObject.name == wallId)
            {
                return child;
            }
        }
        return null;
    }
}
