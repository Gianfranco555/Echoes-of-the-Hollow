using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utilities for setting up the main level scene based on a HousePlanSO asset.
/// </summary>
public static class SceneSetupHelper
{
    private const string ScenePath = "Assets/Scenes/House_MainLevel.unity";
    private const string HousePlanPath = "Assets/BlueprintData/MyHousePlan.asset";

    [MenuItem("House Tools/Setup Main Level Scene")]
    private static void SetupMainLevelScene()
    {
        Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid())
        {
            bool clear = EditorUtility.DisplayDialog(
                "Scene Exists",
                "House_MainLevel already exists. Clear existing contents?",
                "Clear", "Cancel");
            if (!clear)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                return;
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject obj in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(obj);
            }
        }
        else
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        var plan = AssetDatabase.LoadAssetAtPath<HousePlanSO>(HousePlanPath);
        if (plan == null)
        {
            Debug.LogError($"Failed to load HousePlanSO at {HousePlanPath}");
            return;
        }

        GameObject foundation = FoundationBuilder.GenerateFoundation(plan);
        if (foundation != null)
        {
            SceneManager.MoveGameObjectToScene(foundation, scene);
        }

        SetupLighting(scene);
        SetupCamera(foundation, plan);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// Creates a directional light resembling sunlight.
    /// </summary>
    /// <param name="scene">Scene to add the light to.</param>
    private static void SetupLighting(Scene scene)
    {
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.956f, 0.839f);
        light.intensity = 1f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        SceneManager.MoveGameObjectToScene(lightObj, scene);
    }

    /// <summary>
    /// Positions the main camera to view the generated foundation.
    /// </summary>
    private static void SetupCamera(GameObject foundation, HousePlanSO plan)
    {
        Camera cam = Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
        }

        Bounds bounds = plan.CalculateBounds();
        Vector3 target = foundation != null
            ? foundation.transform.position + bounds.center
            : bounds.center;
        float size = Mathf.Max(bounds.size.x, bounds.size.z);
        Vector3 position = target + new Vector3(-size, size, -size);
        cam.transform.position = position;
        cam.transform.LookAt(target);
    }
}
