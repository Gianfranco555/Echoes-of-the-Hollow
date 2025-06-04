using UnityEngine;

public class AtticEntry : MonoBehaviour
{
    // These would ideally be dynamically found or passed in,
    // but for a simple teleport, we can use a placeholder or relative offset.
    public Vector3 atticTeleportTargetPosition = new Vector3(0, 5.0f, 0); // Example: X,Z from ladder, Y in attic
    public Quaternion atticTeleportTargetRotation = Quaternion.identity;

    private AtticLadderController _ladderController;

    void Start()
    {
        // Assuming this script is on a child of the object with AtticLadderController,
        // or on the same object if the ladder visual itself handles interaction.
        // For the plan, it's on 'deployedLadderVisual', which is a child.
        _ladderController = GetComponentInParent<AtticLadderController>();
        if (_ladderController == null)
        {
            Debug.LogError("AtticEntry: Could not find AtticLadderController in parent.", this);
        }
    }

    public void Interact()
    {
        if (_ladderController == null)
        {
            Debug.LogError("AtticEntry: Ladder controller not found.", this);
            return;
        }

        if (!_ladderController.isDeployed)
        {
            Debug.Log("AtticEntry: Ladder is not deployed. Cannot enter attic.", this);
            return;
        }

        Camera playerCamera = Camera.main;
        if (playerCamera != null)
        {
            // For a simple teleport, we might want the player object itself to move,
            // not just the camera, if the camera is parented to a player controller.
            Transform playerTransform = playerCamera.transform.root; // Get the root player object

            // Calculate target position:
            // Use the ladder's base XZ position, and the specified Y for the attic.
            // This assumes the ladder's transform is at the hatch location.
            Vector3 ladderBasePosition = _ladderController.transform.position;
            Vector3 targetPosition = new Vector3(ladderBasePosition.x, atticTeleportTargetPosition.y, ladderBasePosition.z);

            // If atticTeleportTargetPosition is meant to be an offset from ladder:
            // Vector3 targetPosition = _ladderController.transform.TransformPoint(atticTeleportTargetPosition);
            // For now, let's use the simpler XZ from ladder, Y from variable.

            Debug.Log($"AtticEntry: Interacted. Teleporting player to {targetPosition}", this);

            // If there's a character controller, direct camera moves might be overridden or cause issues.
            // It's often better to move the CharacterController component.
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null)
            {
                // Disable and re-enable CharacterController to force position update
                cc.enabled = false;
                playerTransform.position = targetPosition;
                playerTransform.rotation = atticTeleportTargetRotation;
                cc.enabled = true;
            }
            else
            {
                // Fallback to moving the root transform if no CharacterController
                playerTransform.position = targetPosition;
                playerTransform.rotation = atticTeleportTargetRotation;
            }
        }
        else
        {
            Debug.LogError("AtticEntry: Main camera not found. Cannot teleport player.", this);
        }
    }
}
