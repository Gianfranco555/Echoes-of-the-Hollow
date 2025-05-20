using UnityEngine;
using UnityEngine.UI;

public class StatusBarsController : MonoBehaviour
{
    public Slider sleepBar;
    public Slider powerBar;

    [Range(0f,1f)] public float sleep = 1f;

    private void Update()
    {
        if (sleepBar != null)
            sleepBar.value = sleep;
    }

    public void SetPower(float normalized)
    {
        if (powerBar != null)
            powerBar.value = Mathf.Clamp01(normalized);
    }
}
