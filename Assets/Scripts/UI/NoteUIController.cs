using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NoteUIController : MonoBehaviour
{
    public GameObject notePanel;
    public TextMeshProUGUI noteText;
    public Animator animator;

    /// <summary>
    /// Display a note with a fade-in animation.
    /// </summary>
    public void Show(string text)
    {
        if (notePanel != null)
            notePanel.SetActive(true);
        if (noteText != null)
            noteText.text = text;
        if (animator != null)
            animator.SetTrigger("FadeIn");
    }

    public void Hide()
    {
        if (animator != null)
            animator.SetTrigger("FadeOut");
    }
}
