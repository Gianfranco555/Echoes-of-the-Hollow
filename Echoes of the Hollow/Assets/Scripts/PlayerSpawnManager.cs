using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    private static int requestedSpawnPointId = -1; // -1 indicates no specific spawn point requested

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Make this a persistent singleton
        }
        else if (Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
    }

    public static void RequestSpawnAt(int spawnPointId)
    {
        requestedSpawnPointId = spawnPointId;
        Debug.Log($"PlayerSpawnManager: Spawn requested at ID {spawnPointId}");
    }

    // Call this method after a new scene is loaded to position the player
    public void AttemptSpawnPlayer(GameObject playerObject)
    {
        if (playerObject == null)
        {
            Debug.LogError("PlayerSpawnManager: Player object is null.");
            return;
        }

        if (requestedSpawnPointId != -1)
        {
            SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
            SpawnPoint targetSpawnPoint = null;

            foreach (SpawnPoint sp in spawnPoints)
            {
                if (sp.spawnId == requestedSpawnPointId)
                {
                    targetSpawnPoint = sp;
                    break;
                }
            }

            if (targetSpawnPoint != null)
            {
                playerObject.transform.position = targetSpawnPoint.transform.position;
                playerObject.transform.rotation = targetSpawnPoint.transform.rotation;
                Debug.Log($"PlayerSpawnManager: Player spawned at {targetSpawnPoint.name} (ID: {requestedSpawnPointId}).");
            }
            else
            {
                Debug.LogWarning($"PlayerSpawnManager: No spawn point found with ID {requestedSpawnPointId} in the current scene. Player will remain at their current position or default scene start.");
            }
            requestedSpawnPointId = -1; // Reset after attempting to spawn
        }
        else
        {
            Debug.Log("PlayerSpawnManager: No specific spawn point requested. Player will remain at their current position or default scene start.");
        }
    }
}
