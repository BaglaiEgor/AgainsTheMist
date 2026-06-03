using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TooltipTextMotion : MonoBehaviour
{
    [SerializeField] private float amplitude = 2f;
    [SerializeField] private float speed = 4f;
    [SerializeField] private float characterPhaseStep = 0.45f;

    private TMP_Text text;
    private float phase;

    public static TooltipTextMotion EnsureOn(TextMeshProUGUI text)
    {
        if (text == null)
            return null;

        TooltipTextMotion motion = text.GetComponent<TooltipTextMotion>();
        if (motion == null)
            motion = text.gameObject.AddComponent<TooltipTextMotion>();

        motion.CacheIfNeeded();
        motion.RefreshBasePosition();
        return motion;
    }

    private void Awake()
    {
        CacheIfNeeded();
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void OnEnable()
    {
        CacheIfNeeded();
        RefreshBasePosition();
    }

    private void OnDisable()
    {
        if (text != null)
            text.ForceMeshUpdate();
    }

    private void LateUpdate()
    {
        if (text == null || !text.gameObject.activeInHierarchy)
            return;

        text.ForceMeshUpdate();

        TMP_TextInfo textInfo = text.textInfo;
        if (textInfo == null || textInfo.characterCount == 0)
            return;

        float time = Time.unscaledTime * Mathf.Max(0.01f, speed) + phase;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            Vector3 offset = new Vector3(0f, Mathf.Sin(time + i * characterPhaseStep) * amplitude, 0f);

            vertices[vertexIndex + 0] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            text.UpdateGeometry(meshInfo.mesh, i);
        }
    }

    public void RefreshBasePosition()
    {
        if (text != null)
            text.ForceMeshUpdate();
    }

    private void CacheIfNeeded()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();
    }
}
