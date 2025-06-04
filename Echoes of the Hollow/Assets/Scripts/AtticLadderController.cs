using UnityEngine;

public class AtticLadderController : MonoBehaviour
{
    public bool isDeployed = false;
    public GameObject foldedLadderVisual;
    public GameObject deployedLadderVisual;

    void Start()
    {
        // Ensure initial state is correctly set based on isDeployed
        UpdateVisuals();
    }

    public void ToggleLadder()
    {
        isDeployed = !isDeployed;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (foldedLadderVisual != null)
        {
            foldedLadderVisual.SetActive(!isDeployed);
        }
        if (deployedLadderVisual != null)
        {
            deployedLadderVisual.SetActive(isDeployed);
        }
    }

    // This method can be called by a player interaction system
    public void Interact()
    {
        ToggleLadder();
    }
}
