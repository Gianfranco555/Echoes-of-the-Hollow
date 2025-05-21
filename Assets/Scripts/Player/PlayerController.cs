using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Basic first-person controller using Unity's New Input System.
/// Requires a CharacterController component on the same GameObject.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    /// <summary>Movement speed in units per second.</summary>
    public float moveSpeed = 5f;
    /// <summary>Mouse look sensitivity.</summary>
    public float mouseSensitivity = 100f;

    private CharacterController characterController;
    private Vector2 moveInput = Vector2.zero;
    private Vector2 lookInput = Vector2.zero;
    private float xRotation = 0f;

    [SerializeField] private Camera playerCamera;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    /// <summary>
    /// Called by the Input System to update movement input.
    /// </summary>
    /// <param name="value">Vector2 movement input.</param>
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    /// <summary>
    /// Called by the Input System to update look input.
    /// </summary>
    /// <param name="value">Vector2 look input.</param>
    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (playerCamera != null)
            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        characterController.Move(move * moveSpeed * Time.deltaTime);
    }
}
