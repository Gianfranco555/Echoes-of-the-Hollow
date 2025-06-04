using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // Required for IEnumerator

public class ScenePortal : MonoBehaviour
{
    public string targetSceneName;
    public int targetSpawnPointId = 0; // Default to 0 if not specified
    public bool unloadCurrentScene = true; // Determines if the current scene should be unloaded

    private bool isLoading = false; // To prevent multiple loads

    void OnTriggerEnter(Collider other)
    {
        if (isLoading) return; // If already loading, do nothing

        if (other.CompareTag("Player")) // Make sure your player GameObject has the "Player" tag
        {
            Debug.Log($"ScenePortal: Player entered trigger. Target scene: {targetSceneName}, Spawn ID: {targetSpawnPointId}");
            isLoading = true;

            // Request spawn point for the player in the next scene
            if (PlayerSpawnManager.Instance != null)
            {
                PlayerSpawnManager.RequestSpawnAt(targetSpawnPointId);
            }
            else
            {
                Debug.LogError("ScenePortal: PlayerSpawnManager instance not found. Make sure a PlayerSpawnManager is in the scene and initialized.");
                isLoading = false;
                return;
            }

            StartCoroutine(LoadTargetScene());
        }
    }

    private IEnumerator LoadTargetScene()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("ScenePortal: Target scene name is not set.");
            isLoading = false;
            yield break;
        }

        // Start loading the target scene additively
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);

        // Wait until the new scene is fully loaded
        while (!asyncLoad.isDone)
        {
            // Here you could update a loading progress bar if you have one
            Debug.Log($"ScenePortal: Loading scene {targetSceneName}... Progress: {asyncLoad.progress * 100}%");
            yield return null;
        }

        Debug.Log($"ScenePortal: Scene {targetSceneName} loaded successfully.");

        // Set the newly loaded scene as the active scene
        // This is important for lighting, NavMesh, and other scene-specific settings
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (targetScene.IsValid())
        {
            SceneManager.SetActiveScene(targetScene);
            Debug.Log($"ScenePortal: Active scene set to {targetSceneName}");
        }
        else
        {
            Debug.LogError($"ScenePortal: Could not find scene {targetSceneName} to set as active after loading.");
            // Not returning here, as the scene is loaded, just couldn't set active.
        }

        // Unload the current scene if specified
        if (unloadCurrentScene)
        {
            string currentSceneName = SceneManager.GetActiveScene().name; // Get current scene name *before* unloading
            // It's safer to get the current scene by gameObject.scene.name if this script is part of the scene to be unloaded
            string sceneNameToUnload = gameObject.scene.name;

            if (!string.IsNullOrEmpty(sceneNameToUnload) && sceneNameToUnload != targetSceneName)
            {
                Debug.Log($"ScenePortal: Unloading current scene: {sceneNameToUnload}");
                AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(sceneNameToUnload);
                while (asyncUnload != null && !asyncUnload.isDone)
                {
                    Debug.Log($"ScenePortal: Unloading scene {sceneNameToUnload}... Progress: {asyncUnload.progress * 100}%");
                    yield return null;
                }
                if(asyncUnload != null) // Check if operation was successful
                   Debug.Log($"ScenePortal: Scene {sceneNameToUnload} unloaded successfully.");
                else
                   Debug.LogError($"ScenePortal: Failed to start unloading scene {sceneNameToUnload}.");

            }
            else if (sceneNameToUnload == targetSceneName)
            {
                 Debug.LogWarning($"ScenePortal: Current scene ({sceneNameToUnload}) is the same as target scene. Not unloading.");
            }
            else
            {
                Debug.LogWarning("ScenePortal: Could not determine current scene name to unload or it's invalid.");
            }
        }

        // Reset loading flag
        isLoading = false;
    }
}
