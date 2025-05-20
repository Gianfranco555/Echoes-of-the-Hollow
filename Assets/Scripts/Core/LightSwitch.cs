using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    private const float BulbWattage = 40f;

    [SerializeField]
    private Light controlledLight;

    public bool IsOn => controlledLight != null && controlledLight.enabled;
    public Light LightComponent => controlledLight;

    private void OnMouseDown()
    {
        Toggle();
    }

    public void Toggle()
    {
        if (controlledLight == null)
            return;

        BreakerBox breaker = HouseManager.Instance != null ? HouseManager.Instance.breakerBox : null;
        if (breaker != null && breaker.isTripped)
            return;

        bool nextState = !controlledLight.enabled;
        controlledLight.enabled = nextState;

        if (HouseManager.Instance != null)
        {
            float delta = nextState ? BulbWattage : -BulbWattage;
            HouseManager.Instance.ReportLoadDelta(delta);
        }
    }
}
