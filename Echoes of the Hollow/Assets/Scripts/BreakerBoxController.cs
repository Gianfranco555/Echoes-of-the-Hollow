using UnityEngine;

public class BreakerBoxController : MonoBehaviour
{
    public bool isPowerOn = true;

    public void TogglePower()
    {
        isPowerOn = !isPowerOn;
    }
}
