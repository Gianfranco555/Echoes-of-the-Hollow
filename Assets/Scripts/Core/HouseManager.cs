using UnityEngine;

public class HouseManager : MonoBehaviour
{
    public static HouseManager Instance { get; private set; }
    [Header("Systems")]
    public ProjectManager projectManager;
    public StatusBarsController statusBars;
    public BreakerBox breakerBox;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
}
