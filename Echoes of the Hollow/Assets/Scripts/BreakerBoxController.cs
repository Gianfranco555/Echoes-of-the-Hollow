using UnityEngine;
using System; // Added for Action

public class BreakerBoxController : MonoBehaviour
{
    public bool isPowerOn = true;
    public event Action<bool> OnPowerStateChanged; // Event for power state changes

    public void TogglePower()
    {
        isPowerOn = !isPowerOn;
        Debug.Log($"Breaker power toggled. New state: {(isPowerOn ? "ON" : "OFF")}"); // Added for clarity
        OnPowerStateChanged?.Invoke(isPowerOn); // Invoke the event
    }
}
