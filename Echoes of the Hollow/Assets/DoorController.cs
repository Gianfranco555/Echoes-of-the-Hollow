using System.Collections;
using UnityEngine;

/// <summary>
/// Handles the opening and closing of a hinged door.
/// </summary>
public class DoorController : MonoBehaviour
{
    [SerializeField] private Vector3 hingeOffset = Vector3.zero;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 90f;

    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [SerializeField] private bool isOpen;

    private bool isAnimating;
    private float currentAngle;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Toggles the door open or closed.
    /// </summary>
    public void ToggleDoorState()
    {
        if (!isAnimating)
        {
            StartCoroutine(AnimateDoor());
        }
    }

    private IEnumerator AnimateDoor()
    {
        isAnimating = true;

        float targetAngle = isOpen ? 0f : openAngle;
        Vector3 hingeWorld = transform.TransformPoint(hingeOffset);
        Vector3 axisWorld = transform.TransformDirection(rotationAxis.normalized);
        AudioClip clip = isOpen ? closeSound : openSound;
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }

        while (!Mathf.Approximately(currentAngle, targetAngle))
        {
            float step = rotationSpeed * Time.deltaTime;
            float remaining = Mathf.Abs(targetAngle - currentAngle);
            if (step > remaining)
            {
                step = remaining;
            }
            step *= Mathf.Sign(targetAngle - currentAngle);
            transform.RotateAround(hingeWorld, axisWorld, step);
            currentAngle += step;
            yield return null;
        }

        isOpen = !isOpen;
        isAnimating = false;
    }
}
