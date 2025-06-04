using UnityEngine;
using System; // Added for Action

public class BreakerBoxController : MonoBehaviour
{
    // Backing field
    private bool _isPowerOn = true;
    public event Action<bool> OnPowerStateChanged; // Event for power state changes

    public bool IsPowerOn
    {
        get => _isPowerOn;
        set
        {
            if (_isPowerOn == value) return; // No change, no action
            _isPowerOn = value;
            Debug.Log($"Breaker power state changed via property. New state: {(_isPowerOn ? "ON" : "OFF")}"); // Updated log message
            OnPowerStateChanged?.Invoke(_isPowerOn);
        }
    }

    public void TogglePower()
    {
        this.IsPowerOn = !this.IsPowerOn; // Use the property setter
    }
}
