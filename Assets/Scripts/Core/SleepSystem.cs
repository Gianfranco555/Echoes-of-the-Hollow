using UnityEngine;
using UnityEngine.Events;

public class SleepSystem : MonoBehaviour
{
    public float maxSleep = 600f;
    public float currentSleep;
    public float depletionRate = 1f;

    [System.Serializable]
    public class SleepEvent : UnityEvent<float> {}
    public SleepEvent OnSleepChanged = new SleepEvent();

    private void Awake()
    {
        currentSleep = maxSleep;
        OnSleepChanged.Invoke(NormalizedSleep);
    }

    public float NormalizedSleep => maxSleep > 0f ? currentSleep / maxSleep : 0f;

    public void AddSleep(float seconds)
    {
        currentSleep = Mathf.Clamp(currentSleep + seconds, 0f, maxSleep);
        OnSleepChanged.Invoke(NormalizedSleep);
    }

    private void Update()
    {
        if (currentSleep <= 0f)
            return;

        currentSleep = Mathf.Max(currentSleep - depletionRate * Time.deltaTime, 0f);
        OnSleepChanged.Invoke(NormalizedSleep);
    }
}
