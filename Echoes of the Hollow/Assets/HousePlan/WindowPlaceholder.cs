using UnityEngine;

/// <summary>
/// Runtime helper that procedurally creates very simple geometry for window
/// placeholder prefabs.
/// </summary>
public class WindowPlaceholder : MonoBehaviour
{
    public enum PlaceholderType
    {
        SingleHung,
        Sliding,
        Bay,
        SkylightQuad
    }

    [SerializeField] private PlaceholderType placeholderType;

    private void Awake()
    {
        BuildPlaceholder();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            BuildPlaceholder();
        }
    }
#endif

    private void BuildPlaceholder()
    {
        switch (placeholderType)
        {
            case PlaceholderType.SingleHung:
            case PlaceholderType.Sliding:
                BuildSimpleFrame();
                break;
            case PlaceholderType.Bay:
                BuildBayFrame();
                break;
            case PlaceholderType.SkylightQuad:
                BuildSkylight();
                break;
        }
    }

    private void BuildSimpleFrame()
    {
        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Frame";
        frame.transform.SetParent(transform, false);
        frame.transform.localScale = new Vector3(1f, 1f, 0.05f);

        GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glass.name = "Glass";
        glass.transform.SetParent(transform, false);
        glass.transform.localScale = new Vector3(0.9f, 0.9f, 0.02f);
        glass.transform.localPosition = Vector3.zero;
    }

    private void BuildBayFrame()
    {
        const float angle = 30f;
        for (int i = 0; i < 3; i++)
        {
            GameObject segment = new GameObject($"Segment_{i}");
            segment.transform.SetParent(transform, false);
            segment.transform.localRotation = Quaternion.Euler(0f, (i - 1) * angle, 0f);

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(segment.transform, false);
            cube.transform.localScale = new Vector3(0.6f, 1f, 0.05f);
        }
    }

    private void BuildSkylight()
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Skylight";
        quad.transform.SetParent(transform, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(1f, 1f, 1f);
    }
}
