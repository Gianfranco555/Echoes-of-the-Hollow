using System.Collections;
using UnityEngine;

/// <summary>
/// Handles opening and closing of a simple sliding door.
/// </summary>
public class SlidingDoorController : MonoBehaviour
{
    [SerializeField] private Transform slidingPanel;
    [SerializeField] private float slideDistance = 0.91f;
    [SerializeField] private bool slidesLeft = true;
    [SerializeField] private float slideSpeed = 2f;
    [SerializeField] private bool isOpen;

    private bool isAnimating;
    private Vector3 closedPosition;
    private Vector3 openPosition;

    private void Awake()
    {
        if (slidingPanel == null)
        {
            if (transform.childCount > 1)
            {
                slidingPanel = transform.GetChild(1);
            }
            else
            {
                Debug.LogError("SlidingDoorController requires a sliding panel", this);
                enabled = false;
                return;
            }
        }

        closedPosition = slidingPanel.localPosition;
        openPosition = closedPosition + (slidesLeft ? Vector3.left : Vector3.right) * slideDistance;
        if (isOpen)
        {
            slidingPanel.localPosition = openPosition;
        }
    }

    /// <summary>
    /// Toggles the door between open and closed states.
    /// </summary>
    public void ToggleDoorState()
    {
        if (!isAnimating && slidingPanel != null)
        {
            StartCoroutine(AnimateDoor());
        }
    }

    private IEnumerator AnimateDoor()
    {
        isAnimating = true;
        Vector3 target = isOpen ? closedPosition : openPosition;
        while (Vector3.Distance(slidingPanel.localPosition, target) > 0.001f)
        {
            slidingPanel.localPosition = Vector3.MoveTowards(slidingPanel.localPosition, target, slideSpeed * Time.deltaTime);
            yield return null;
        }
        isOpen = !isOpen;
        isAnimating = false;
    }

    private void OnValidate()
    {
        if (slidingPanel == null && transform.childCount > 1)
        {
            slidingPanel = transform.GetChild(1);
        }
        if (slidingPanel == null)
        {
            return;
        }
        closedPosition = slidingPanel.localPosition;
        openPosition = closedPosition + (slidesLeft ? Vector3.left : Vector3.right) * slideDistance;
        slidingPanel.localPosition = isOpen ? openPosition : closedPosition;
    }
}
