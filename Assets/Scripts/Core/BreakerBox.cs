using System.Collections.Generic;
using UnityEngine;

public class BreakerBox : MonoBehaviour
{
    public float maxLoad = 200f;
    public bool isTripped = false;

    private float cumulativeLoad = 0f;
    private readonly List<Light> previouslyOnLights = new List<Light>();

    public void ReportLoadDelta(float delta)
    {
        if (isTripped)
            return;

        cumulativeLoad += delta;
        if (cumulativeLoad > maxLoad)
            Trip();
    }

    private void Trip()
    {
        isTripped = true;
        cumulativeLoad = 0f;
        previouslyOnLights.Clear();

        foreach (Light l in FindObjectsOfType<Light>())
        {
            if (l.enabled)
                previouslyOnLights.Add(l);
            l.enabled = false;
        }

        if (HouseManager.Instance != null)
            HouseManager.Instance.SetLoad(0f);
    }

    private void ResetBreaker()
    {
        isTripped = false;
        cumulativeLoad = 0f;
        if (HouseManager.Instance != null)
            HouseManager.Instance.SetLoad(0f);

        foreach (Light l in previouslyOnLights)
        {
            if (l != null)
            {
                l.enabled = true;
                if (HouseManager.Instance != null)
                    HouseManager.Instance.ReportLoadDelta(40f);
            }
        }

        previouslyOnLights.Clear();
    }

    private void OnMouseDown()
    {
        if (isTripped)
            ResetBreaker();
    }
}
