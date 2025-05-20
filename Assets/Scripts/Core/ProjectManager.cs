using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ProjectManager : MonoBehaviour
{
    public List<Project> activeProjects = new List<Project>();

    [System.Serializable]
    public class ProgressEvent : UnityEvent<float> {}
    public ProgressEvent OnProgressChanged = new ProgressEvent();

    public void WorkOnProject(int index, float delta)
    {
        if (index < 0 || index >= activeProjects.Count)
            return;
        Project proj = activeProjects[index];
        if (proj.isComplete || proj.isExpired)
            return;

        float prev = proj.progress;
        proj.progress = Mathf.Clamp01(proj.progress + proj.workRate * delta);
        if (proj.progress >= 1f)
        {
            proj.progress = 1f;
            proj.isComplete = true;
        }
        activeProjects[index] = proj;
        if (proj.progress != prev)
            OnProgressChanged.Invoke(proj.progress);
    }

    public void Tick(float delta)
    {
        for (int i = 0; i < activeProjects.Count; i++)
        {
            Project proj = activeProjects[i];
            if (proj.isComplete || proj.isExpired)
                continue;

            proj.deadlineSeconds -= delta;
            if (proj.deadlineSeconds <= 0f)
            {
                proj.deadlineSeconds = 0f;
                if (proj.progress < 1f)
                {
                    proj.isExpired = true;
                    proj.reward = 0f;
                }
            }
            activeProjects[i] = proj;
        }
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    private void Start()
    {
        if (activeProjects.Count == 0)
        {
            activeProjects.Add(new Project
            {
                title = "Logo Cleanup",
                deadlineSeconds = 180f,
                reward = 50f,
                workRate = 0.003f
            });
        }
    }
}
