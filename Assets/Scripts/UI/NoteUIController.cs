using UnityEngine;
using UnityEngine.UI;

public class NoteUIController : MonoBehaviour
{
    public GameObject notePanel;
    public Text noteText;

    public void ShowNote(string text)
    {
        if (notePanel != null)
            notePanel.SetActive(true);
        if (noteText != null)
            noteText.text = text;
    }

    public void HideNote()
    {
        if (notePanel != null)
            notePanel.SetActive(false);
    }
}
