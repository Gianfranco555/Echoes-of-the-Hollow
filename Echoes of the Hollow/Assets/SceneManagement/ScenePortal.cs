using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Triggers scene transitions when the player enters the portal.
/// </summary>
public class ScenePortal : MonoBehaviour
{
    [Tooltip("Name of the scene to load when triggered.")]
    public string targetSceneName;

    [Tooltip("Id of the spawn point to use in the target scene.")]
    public int targetSpawnPointId;

    [Tooltip("Unload the current scene after loading the target.")]
    [SerializeField] private bool unloadCurrentScene = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        StartCoroutine(Transition());
    }

    private IEnumerator Transition()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            yield break;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        while (!load.isDone)
        {
            yield return null;
        }

        if (unloadCurrentScene)
        {
            Scene current = gameObject.scene;
            AsyncOperation unload = SceneManager.UnloadSceneAsync(current);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }
        }

        PlayerSpawnManager.RequestSpawnAt(targetSpawnPointId);
    }
}
