using System.Collections;
using UnityEngine;

/// <summary>
/// Basic behaviour for the stalker that appears outside the house.
/// </summary>
public class StalkerAI : MonoBehaviour
{
    [Tooltip("Possible windows the stalker can appear at")]
    public Transform[] windowPositions;

    [Tooltip("Library of notes the stalker can leave")]
    public NoteLibrary noteLibrary;

    [Tooltip("Controller used to display notes to the player")]
    public NoteUIController noteUI;

    [Tooltip("Audio source used for the camera flash sound")]
    public AudioSource cameraFlashSource;

    public float baseInterval = 45f;

    private int noteCount = 0;
    public int NoteCount => noteCount;

    private void Start()
    {
        StartCoroutine(Loop());
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(baseInterval / (1f + noteCount));

            if (windowPositions != null && windowPositions.Length > 0)
            {
                Transform t = windowPositions[Random.Range(0, windowPositions.Length)];
                if (t != null)
                    transform.position = t.position;
            }

            if (cameraFlashSource != null)
                cameraFlashSource.Play();

            yield return new WaitForSeconds(2f);

            if (noteLibrary != null && noteLibrary.lines != null && noteLibrary.lines.Length > 0)
            {
                string line = noteLibrary.lines[Random.Range(0, noteLibrary.lines.Length)];
                NoteScriptableObject note = ScriptableObject.CreateInstance<NoteScriptableObject>();
                note.text = line;
                if (noteUI != null)
                    noteUI.Show(note.text);
            }

            noteCount++;
        }
    }
}
