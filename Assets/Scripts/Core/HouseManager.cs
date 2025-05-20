using UnityEngine;

public class HouseManager : MonoBehaviour
{
    public static HouseManager Instance { get; private set; }
    [Header("Systems")]
    public ProjectManager projectManager;
    public StatusBarsController statusBars;
    public BreakerBox breakerBox;

    public float currentLoad = 0f;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void ReportLoadDelta(float watts)
    {
        currentLoad = Mathf.Max(currentLoad + watts, 0f);
        if (breakerBox != null)
            breakerBox.ReportLoadDelta(watts);
    }

    public void SetLoad(float watts)
    {
        currentLoad = Mathf.Max(watts, 0f);
    }

    private void Update()
    {
        if (statusBars != null && breakerBox != null)
        {
            float normalized = breakerBox.maxLoad > 0f ? currentLoad / breakerBox.maxLoad : 0f;
            statusBars.SetPower(normalized);
        }
    }
}
