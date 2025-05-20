using UnityEngine;
using UnityEngine.UI;

public class StatusBarsController : MonoBehaviour
{
    public Slider sleepBar;
    public Slider powerBar;

    [Range(0f,1f)] public float sleep = 1f;
    [Range(0f,1f)] public float power = 1f;

    private void Update()
    {
        if (sleepBar != null)
            sleepBar.value = sleep;
        if (powerBar != null)
            powerBar.value = power;
    }
}
