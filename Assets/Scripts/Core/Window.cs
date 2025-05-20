using UnityEngine;

public class Window : MonoBehaviour
{
    public bool isOpen = false;

    public void ToggleWindow()
    {
        isOpen = !isOpen;
    }
}
