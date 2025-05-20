using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProjectUIController : MonoBehaviour
{
    public ProjectManager projectManager;
    public RectTransform rowsParent;
    public GameObject rowPrefab;

    private readonly List<Row> rows = new List<Row>();

    private class Row
    {
        public Text title;
        public Slider progress;
        public Button workButton;
        public int index;
    }

    private void Start()
    {
        BuildRows();
    }

    private void BuildRows()
    {
        if (projectManager == null || rowsParent == null || rowPrefab == null)
            return;

        rows.Clear();
        for (int i = 0; i < projectManager.activeProjects.Count; i++)
        {
            GameObject go = Instantiate(rowPrefab, rowsParent);
            Row r = new Row();
            r.index = i;
            r.title = go.GetComponentInChildren<Text>();
            r.progress = go.GetComponentInChildren<Slider>();
            r.workButton = go.GetComponentInChildren<Button>();
            rows.Add(r);
        }
    }

    private void Update()
    {
        if (projectManager == null)
            return;

        for (int i = 0; i < rows.Count && i < projectManager.activeProjects.Count; i++)
        {
            Project p = projectManager.activeProjects[i];
            Row r = rows[i];

            if (r.title != null)
                r.title.text = p.title;
            if (r.progress != null)
                r.progress.value = p.progress;
            if (r.workButton != null && r.workButton.IsPressed())
                projectManager.WorkOnProject(r.index, Time.deltaTime);
        }
    }
}
