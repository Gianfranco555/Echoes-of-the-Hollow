using UnityEngine;

public class ProjectManager : MonoBehaviour
{
    [Range(0f,1f)]
    public float projectProgress = 0f;
    public float projectDeadline = 300f; // seconds

    public void UpdateProgress(float amount)
    {
        projectProgress = Mathf.Clamp01(projectProgress + amount);
    }
}
