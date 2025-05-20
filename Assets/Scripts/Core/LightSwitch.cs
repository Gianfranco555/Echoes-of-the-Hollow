using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    public Light controlledLight;

    public void Toggle()
    {
        if (controlledLight != null)
            controlledLight.enabled = !controlledLight.enabled;
    }
}
