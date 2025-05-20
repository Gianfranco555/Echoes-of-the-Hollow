using UnityEngine;

public class Door : MonoBehaviour
{
    public bool isOpen = false;

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }
}
