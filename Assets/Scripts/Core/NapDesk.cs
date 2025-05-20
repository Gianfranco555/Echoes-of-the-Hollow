using System.Collections;
using UnityEngine;

public class NapDesk : MonoBehaviour
{
    public SleepSystem sleepSystem;
    public CanvasGroup blackoutGroup;
    public float napSeconds = 5f;
    public MonoBehaviour[] controlsToDisable;

    private bool isNapping = false;

    private void Update()
    {
        if (isNapping || sleepSystem == null)
            return;

        if (Input.GetKeyDown(KeyCode.N))
            StartCoroutine(NapRoutine());
    }

    private IEnumerator NapRoutine()
    {
        isNapping = true;
        ToggleControls(false);
        if (blackoutGroup != null)
            blackoutGroup.alpha = 1f;

        if (sleepSystem != null)
            sleepSystem.AddSleep(60f);

        yield return new WaitForSeconds(napSeconds);

        if (blackoutGroup != null)
            blackoutGroup.alpha = 0f;
        ToggleControls(true);
        isNapping = false;
    }

    private void ToggleControls(bool state)
    {
        if (controlsToDisable == null)
            return;

        foreach (var c in controlsToDisable)
        {
            if (c != null)
                c.enabled = state;
        }
    }
}
