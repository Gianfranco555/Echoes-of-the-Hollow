using UnityEngine;
using UnityEngine.UI;

public class StatusBarsController : MonoBehaviour
{
    public Slider sleepBar;
    public Slider powerBar;

    public void SetSleep(float normalized)
    {
        if (sleepBar == null)
            return;

        normalized = Mathf.Clamp01(normalized);
        sleepBar.value = normalized;

        Image fill = sleepBar.fillRect != null ? sleepBar.fillRect.GetComponent<Image>() : null;
        if (fill != null)
            fill.color = normalized < 0.25f ? Color.red : Color.white;
    }

    public void SetPower(float normalized)
    {
        if (powerBar != null)
            powerBar.value = Mathf.Clamp01(normalized);
    }
}
