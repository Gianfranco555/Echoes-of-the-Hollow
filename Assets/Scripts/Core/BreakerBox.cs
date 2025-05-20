using UnityEngine;

public class BreakerBox : MonoBehaviour
{
    public bool powerOn = true;

    public void TogglePower()
    {
        powerOn = !powerOn;
    }
}
