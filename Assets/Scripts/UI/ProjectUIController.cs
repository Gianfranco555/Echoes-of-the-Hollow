using UnityEngine;
using UnityEngine.UI;

public class ProjectUIController : MonoBehaviour
{
    public ProjectManager projectManager;
    public Slider progressBar;

    private void Update()
    {
        if (projectManager != null && progressBar != null)
            progressBar.value = projectManager.projectProgress;
    }
}
