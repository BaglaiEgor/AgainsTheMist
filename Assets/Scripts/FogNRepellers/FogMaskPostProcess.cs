using UnityEngine;

[ExecuteAlways]
public class FogMaskPostProcess : MonoBehaviour
{
    [Range(0, 1)] public float sharpness = 0.5f;
    [Range(0.5f, 3)] public float contrast = 1.2f;

    private Material postProcessMaterial;

    void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        CreateMaterial();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled || !Application.isPlaying) return;
        CreateMaterial();
    }

    void CreateMaterial()
    {
        if (postProcessMaterial == null)
        {
            var shader = Shader.Find("Hidden/FogMaskPostProcess");
            if (shader != null)
            {
                postProcessMaterial = new Material(shader);
                postProcessMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }

    public void ApplyPostProcess(RenderTexture source, RenderTexture destination)
    {
        if (postProcessMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        postProcessMaterial.SetFloat("_Sharpness", sharpness);
        postProcessMaterial.SetFloat("_Contrast", contrast);

        Graphics.Blit(source, destination, postProcessMaterial);
    }

    void OnDisable()
    {
        if (postProcessMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(postProcessMaterial);
            else
                DestroyImmediate(postProcessMaterial);
            postProcessMaterial = null;
        }
    }
}
