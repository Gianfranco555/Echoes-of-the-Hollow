using UnityEngine;

public class HouseManager : MonoBehaviour
{
    public static HouseManager Instance { get; private set; }
    [Header("Systems")]
    public ProjectManager projectManager;
    public StatusBarsController statusBars;
    public BreakerBox breakerBox;
    public SleepSystem sleepSystem;

    [Header("Threats")]
    public StalkerAI stalkerAI;

    [Header("UI")]
    public GameObject gameOverPanel;

    public float currentLoad = 0f;
    private bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (sleepSystem == null)
            sleepSystem = gameObject.AddComponent<SleepSystem>();

        if (sleepSystem != null && statusBars != null)
            sleepSystem.OnSleepChanged.AddListener(statusBars.SetSleep);
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

        if (!isGameOver && stalkerAI != null && stalkerAI.NoteCount >= 3 && HasOpenEntry())
            GameOver("Stalker Break-In");
    }

    private bool HasOpenEntry()
    {
        foreach (Door d in FindObjectsOfType<Door>())
        {
            if (d.isOpen)
                return true;
        }

        foreach (Window w in FindObjectsOfType<Window>())
        {
            if (w.isOpen)
                return true;
        }

        return false;
    }

    private void GameOver(string reason)
    {
        isGameOver = true;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        Debug.Log(reason);
    }
}
