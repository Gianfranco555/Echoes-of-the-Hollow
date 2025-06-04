using UnityEngine;
using UnityEngine.SceneManagement; // Required for sceneLoaded event

public class Movement : MonoBehaviour
{
    [SerializeField] CharacterController controller;
    [SerializeField] float speed = 11f;
    Vector2 horizontalInput;

    [SerializeField] float jumpHeight = 3.5f;
    bool jump;
    [SerializeField] float gravity = -30f;
    Vector3 verticalVelocity = Vector3.zero;

    [SerializeField] LayerMask groundMask;
    bool isGrounded;

    void Awake()
    {
        // It's good practice to ensure the CharacterController is assigned.
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }
        // Subscribe to the sceneLoaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        // Unsubscribe when the object is destroyed to prevent memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        // Initial attempt to spawn at a specific point if this is the first scene load for the player
        // or if the scene was reloaded without a portal.
        if (PlayerSpawnManager.Instance != null)
        {
            PlayerSpawnManager.Instance.AttemptSpawnPlayer(gameObject);
        }
        else
        {
            Debug.LogWarning("Movement Start: PlayerSpawnManager instance not found. Player will start at default position.");
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Movement: Scene {scene.name} loaded. Mode: {mode}. Attempting to spawn player.");
        if (PlayerSpawnManager.Instance != null)
        {
            // Disable CharacterController temporarily to allow teleportation
            if (controller != null) controller.enabled = false;
            PlayerSpawnManager.Instance.AttemptSpawnPlayer(gameObject);
            if (controller != null) controller.enabled = true;
        }
        else
        {
            Debug.LogWarning($"Movement OnSceneLoaded: PlayerSpawnManager instance not found in scene {scene.name}. Player will remain at default position or last position.");
        }
    }

    private void Update (){
        isGrounded = Physics.CheckSphere(transform.position, 0.1f, groundMask);
        if (isGrounded){
            verticalVelocity.y = 0;
        }

        // Example movement logic
        if (controller != null && controller.enabled)
        {
            Vector3 horizontalVelocity = (transform.right * horizontalInput.x + transform.forward * horizontalInput.y) * speed;
            controller.Move(horizontalVelocity * Time.deltaTime);

            if (jump){
                if (isGrounded){
                    verticalVelocity.y = Mathf.Sqrt(-2f * jumpHeight * gravity);
                }
                jump = false;
            }

            verticalVelocity.y += gravity * Time.deltaTime;
            controller.Move(verticalVelocity * Time.deltaTime);
        }
    }

    public void ReceiveInput (Vector2 _horizontalInput){
        horizontalInput = _horizontalInput;
        Debug.Log(horizontalInput);
    }

    public void OnJumpPressed (){
        jump = true;
    }
}
