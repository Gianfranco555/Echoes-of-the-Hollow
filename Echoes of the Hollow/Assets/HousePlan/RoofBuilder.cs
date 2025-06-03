using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility for procedurally generating simple roof meshes based on a <see cref="HousePlanSO"/>.
/// </summary>
public static class RoofBuilder
{
    /// <summary>
    /// Generates roof geometry for the supplied plan. Separate mesh sections are
    /// created for the main gable, garage lean-to and the covered patio/entry.
    /// </summary>
    /// <param name="plan">House plan describing the building layout.</param>
    /// <param name="wallContainer">Container with exterior wall renderers used to calculate bounds.</param>
    /// <returns>Root GameObject containing all generated roof sections.</returns>
    public static GameObject GenerateRoof(HousePlanSO plan, GameObject wallContainer)
    {
        if (plan == null)
        {
            Debug.LogError("HousePlanSO is null. Cannot generate roof.");
            return null;
        }

        GameObject root = new GameObject("Roof_Generated");
        float eaveHeight = plan.storyHeight;

        // Determine overall extents using existing wall geometry if available.
        Bounds houseBounds = CalculateBoundsFromWalls(wallContainer);
        if (houseBounds.size == Vector3.zero)
        {
            houseBounds = plan.CalculateBounds();
        }

        // Main-house gable section -------------------------------------------------
        Bounds mainBounds = CalculateRoomBounds(plan, new[] { "Garage", "CoveredPatio", "CoveredEntry" }, true, houseBounds);
        float pitchMain = 6f / 12f;
        float pitchMainDeg = Mathf.Rad2Deg * Mathf.Atan(pitchMain);
        float mainRidgeHeight = eaveHeight + Mathf.Tan(Mathf.Deg2Rad * pitchMainDeg) * (mainBounds.size.z * 0.5f);
        GameObject mainGable = BuildGableSection("Roof_MainGable",
                                                 new Vector2(mainBounds.min.x, mainBounds.min.z),
                                                 mainBounds.size.x,
                                                 mainBounds.size.z,
                                                 eaveHeight,
                                                 mainRidgeHeight);
        mainGable.transform.SetParent(root.transform, false);

        // Garage lean-to section ---------------------------------------------------
        Bounds garageBounds = CalculateRoomBounds(plan, new[] { "Garage" }, false, houseBounds);
        float pitchGarage = 3f / 12f;
        float pitchGarageDeg = Mathf.Rad2Deg * Mathf.Atan(pitchGarage);
        float garageLowHeight = plan.storyHeight - Mathf.Tan(Mathf.Deg2Rad * pitchGarageDeg) * garageBounds.size.z;
        GameObject garageRoof = BuildMonoSection("Roof_Garage",
                                               new Vector2(garageBounds.min.x, garageBounds.min.z),
                                               garageBounds.size.x,
                                               garageBounds.size.z,
                                               garageLowHeight,
                                               plan.storyHeight,
                                               false);
        garageRoof.transform.SetParent(root.transform, false);

        // Covered patio and entry section -----------------------------------------
        Bounds patioBounds = CalculateRoomBounds(plan, new[] { "CoveredPatio", "CoveredEntry" }, false, houseBounds);
        float pitchPatio = 1f / 12f;
        float pitchPatioDeg = Mathf.Rad2Deg * Mathf.Atan(pitchPatio);
        float patioLow = plan.storyHeight - Mathf.Tan(Mathf.Deg2Rad * pitchPatioDeg) * patioBounds.size.z;
        GameObject patioRoof = BuildMonoSection("Roof_PatioEntry",
                                              new Vector2(patioBounds.min.x, patioBounds.min.z),
                                              patioBounds.size.x,
                                              patioBounds.size.z,
                                              patioLow,
                                              plan.storyHeight,
                                              false);
        patioRoof.transform.SetParent(root.transform, false);

        return root;
    }

    //-------------------------------------------------------------------------
    private static Bounds CalculateBoundsFromWalls(GameObject wallContainer)
    {
        if (wallContainer == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Renderer[] renderers = wallContainer.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            b.Encapsulate(renderers[i].bounds);
        }
        return b;
    }

    private static Bounds CalculateRoomBounds(HousePlanSO plan, IEnumerable<string> roomIds, bool invertSelection, Bounds fallback)
    {
        bool hasBounds = false;
        Bounds b = new Bounds();
        foreach (RoomData room in plan.rooms)
        {
            bool contains = false;
            foreach (string id in roomIds)
            {
                if (room.roomId == id)
                {
                    contains = true;
                    break;
                }
            }

            if (invertSelection ? contains : !contains)
            {
                continue;
            }

            Vector3 min = room.position;
            Vector3 size = new Vector3(room.dimensions.x, 0f, room.dimensions.y);
            Bounds rb = new Bounds(min + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f), size);
            if (!hasBounds)
            {
                b = rb;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(rb);
            }
        }

        if (!hasBounds)
        {
            return fallback;
        }
        return b;
    }

    private static GameObject BuildGableSection(string name, Vector2 start, float width, float depth, float eaveHeight, float ridgeHeight)
    {
        Vector3[] vertices = new Vector3[6];
        vertices[0] = new Vector3(0f, eaveHeight, 0f);
        vertices[1] = new Vector3(width, eaveHeight, 0f);
        vertices[2] = new Vector3(0f, eaveHeight, depth);
        vertices[3] = new Vector3(width, eaveHeight, depth);
        vertices[4] = new Vector3(0f, ridgeHeight, depth * 0.5f);
        vertices[5] = new Vector3(width, ridgeHeight, depth * 0.5f);

        int[] triangles = new int[]
        {
            0, 1, 5, 0, 5, 4, // South slope
            2, 4, 5, 2, 5, 3  // North slope
        };

        Vector2[] uvs = new Vector2[6]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f)
        };

        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            uv = uvs
        };
        mesh.RecalculateNormals();

        GameObject obj = new GameObject(name);
        MeshFilter filter = obj.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        obj.AddComponent<MeshRenderer>();
        obj.transform.position = new Vector3(start.x, 0f, start.y);
        return obj;
    }

    private static GameObject BuildMonoSection(string name, Vector2 start, float width, float depth, float lowHeight, float highHeight, bool highAtStart)
    {
        Vector3[] vertices = new Vector3[4];
        if (highAtStart)
        {
            vertices[0] = new Vector3(0f, highHeight, 0f);
            vertices[1] = new Vector3(width, highHeight, 0f);
            vertices[2] = new Vector3(0f, lowHeight, depth);
            vertices[3] = new Vector3(width, lowHeight, depth);
        }
        else
        {
            vertices[0] = new Vector3(0f, lowHeight, 0f);
            vertices[1] = new Vector3(width, lowHeight, 0f);
            vertices[2] = new Vector3(0f, highHeight, depth);
            vertices[3] = new Vector3(width, highHeight, depth);
        }

        int[] triangles = new int[]
        {
            0, 2, 1, 1, 2, 3
        };

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles,
            uv = uvs
        };
        mesh.RecalculateNormals();

        GameObject obj = new GameObject(name);
        MeshFilter filter = obj.AddComponent<MeshFilter>();
        filter.mesh = mesh;
        obj.AddComponent<MeshRenderer>();
        obj.transform.position = new Vector3(start.x, 0f, start.y);
        return obj;
    }
}
