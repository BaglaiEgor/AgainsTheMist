using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class FogMaskDrawer : MonoBehaviour
{
    public int textureSize = 512;
    public FilterMode filterMode = FilterMode.Bilinear;
    public bool forceSmoothFiltering = true;
    public Color backgroundColor = Color.black;
    public Color repellerColor = Color.white;
    public float updateRate = 10f;
    public Material fogMaterial;
    public Transform fogSprite;
    [Range(0, 1)] public float edgeSoftness = 0.3f;
    [Range(0, 1)] public float materialMaskSoftness = 0.25f;
    [Range(0.5f, 3f)] public float materialMaskContrast = 1f;

    private RenderTexture fogMaskRT;
    private Material drawMaterial;
    private Camera renderCamera;
    private float timer;
    private Vector2 fogScale = Vector2.one;
    private int cachedTextureSize = -1;
    private FilterMode cachedFilterMode = FilterMode.Bilinear;

    private static readonly int FogMaskId = Shader.PropertyToID("_FogMask");
    private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
    private static readonly int MaskContrastId = Shader.PropertyToID("_MaskContrast");
    private const float RadiusLockedMaskSoftness = 1f;

    void OnEnable()
    {
        InitializeOrRefresh(true);
        ForceUpdate();
    }

    void OnValidate()
    {
        textureSize = Mathf.Max(32, textureSize);
        updateRate = Mathf.Max(0f, updateRate);

        if (!isActiveAndEnabled)
            return;

        InitializeOrRefresh(true);
        ForceUpdate();
    }

    void InitializeOrRefresh(bool forceTextureRecreate = false)
    {
        if (renderCamera == null)
            renderCamera = GetComponent<Camera>();

        if (renderCamera != null)
        {
            renderCamera.orthographic = true;
            renderCamera.clearFlags = CameraClearFlags.Color;
            renderCamera.backgroundColor = backgroundColor;
            renderCamera.cullingMask = 0;
            renderCamera.enabled = false;
            renderCamera.targetTexture = null;
        }

        if (drawMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader != null)
                drawMaterial = CreateInternalColoredMaterial(shader, BlendMode.One, BlendMode.OneMinusSrcColor);
        }

        FilterMode effectiveFilterMode = forceSmoothFiltering ? FilterMode.Bilinear : filterMode;
        bool textureChanged = cachedTextureSize != textureSize || cachedFilterMode != effectiveFilterMode;

        if (forceTextureRecreate || fogMaskRT == null || textureChanged)
        {
            ReleaseRenderTexture();

            fogMaskRT = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            fogMaskRT.filterMode = effectiveFilterMode;
            fogMaskRT.wrapMode = TextureWrapMode.Clamp;
            fogMaskRT.useMipMap = false;
            fogMaskRT.autoGenerateMips = false;
            fogMaskRT.Create();

            cachedTextureSize = textureSize;
            cachedFilterMode = effectiveFilterMode;
        }

        GetFogScale();
        ApplyFogMaterialParameters();
    }

    void GetFogScale()
    {
        if (fogSprite != null)
        {
            fogScale = new Vector2(fogSprite.localScale.x, fogSprite.localScale.y);
            return;
        }

        GameObject fog = null;
        try
        {
            fog = GameObject.FindGameObjectWithTag("Fog");
        }
        catch (UnityException)
        {
            fog = null;
        }

        if (fog != null)
        {
            fogSprite = fog.transform;
            fogScale = new Vector2(fog.transform.localScale.x, fog.transform.localScale.y);
        }
        else
        {
            fogScale = new Vector2(100f, 100f);
        }
    }

    void Update()
    {
        InitializeOrRefresh();

        if (updateRate <= 0f)
            return;

        float delta = Application.isPlaying ? Time.deltaTime : (1f / 30f);
        timer += delta;
        if (timer >= 1f / updateRate)
        {
            RenderFogMask();
            timer = 0f;
        }
    }

    void RenderFogMask()
    {
        if (fogMaskRT == null || drawMaterial == null)
            return;

        FogSystem fogSystem = FogSystem.Instance;
        if (fogSystem == null)
            return;

        if (!Application.isPlaying)
            fogSystem.RebuildRepellersFromScene();

        var repellers = fogSystem.ActiveRepellers;

        RenderTexture previousRT = RenderTexture.active;
        RenderTexture.active = fogMaskRT;
        GL.Clear(true, true, backgroundColor);

        GL.PushMatrix();
        GL.LoadOrtho();

        drawMaterial.SetPass(0);
        for (int i = 0; i < repellers.Count; i++)
        {
            FogRepeller repeller = repellers[i];
            if (repeller == null || !repeller.isActiveAndEnabled)
                continue;

            DrawRepeller(repeller);
        }

        GL.PopMatrix();
        RenderTexture.active = previousRT == fogMaskRT ? null : previousRT;

        FogMaskPostProcess postProcess = GetComponent<FogMaskPostProcess>();
        if (postProcess != null)
        {
            RenderTexture processedRT = RenderTexture.GetTemporary(fogMaskRT.width, fogMaskRT.height, 0, fogMaskRT.format);
            processedRT.filterMode = fogMaskRT.filterMode;

            postProcess.ApplyPostProcess(fogMaskRT, processedRT);
            Graphics.Blit(processedRT, fogMaskRT);

            RenderTexture.ReleaseTemporary(processedRT);
        }

        ApplyFogMaterialParameters();
    }

    Material CreateInternalColoredMaterial(Shader shader, BlendMode srcBlend, BlendMode dstBlend)
    {
        Material material = new Material(shader);
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetInt("_SrcBlend", (int)srcBlend);
        material.SetInt("_DstBlend", (int)dstBlend);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZWrite", 0);
        return material;
    }

    void DrawRepeller(FogRepeller repeller)
    {
        Vector3 drawPosition = repeller.GetDrawPosition();

        float uvX = drawPosition.x / fogScale.x + 0.5f;
        float uvY = drawPosition.y / fogScale.y + 0.5f;

        float minScale = Mathf.Max(0.0001f, Mathf.Min(fogScale.x, fogScale.y));
        float radiusUV = repeller.clearRadius / minScale;
        float transitionPixels = Mathf.Lerp(1f, 12f, edgeSoftness);
        float transitionUV = transitionPixels / Mathf.Max(1f, textureSize);
        float halfTransitionUV = transitionUV * 0.5f;
        float coreRadiusUV = Mathf.Max(0f, radiusUV - halfTransitionUV);

        int segments = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(64f, 192f, edgeSoftness)), 32, 192);
        int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, 20f, edgeSoftness)), 2, 24);

        GL.Begin(GL.TRIANGLES);
        GL.Color(Color.white);
        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.PI * 2f / segments;
            float angle2 = (i + 1) * Mathf.PI * 2f / segments;

            GL.Vertex3(uvX, uvY, 0f);
            GL.Vertex3(uvX + Mathf.Cos(angle1) * coreRadiusUV, uvY + Mathf.Sin(angle1) * coreRadiusUV, 0f);
            GL.Vertex3(uvX + Mathf.Cos(angle2) * coreRadiusUV, uvY + Mathf.Sin(angle2) * coreRadiusUV, 0f);
        }
        GL.End();

        for (int s = 0; s < steps; s++)
        {
            float t0 = (float)s / steps;
            float t1 = (float)(s + 1) / steps;

            float r0 = coreRadiusUV + t0 * transitionUV;
            float r1 = coreRadiusUV + t1 * transitionUV;

            float alpha0 = Mathf.SmoothStep(1f, 0f, t0);
            float alpha1 = Mathf.SmoothStep(1f, 0f, t1);

            Color c0 = new Color(alpha0, alpha0, alpha0, alpha0);
            Color c1 = new Color(alpha1, alpha1, alpha1, alpha1);

            GL.Begin(GL.TRIANGLE_STRIP);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                GL.Color(c0);
                GL.Vertex3(uvX + cos * r0, uvY + sin * r0, 0f);

                GL.Color(c1);
                GL.Vertex3(uvX + cos * r1, uvY + sin * r1, 0f);
            }
            GL.End();
        }
    }

    void ApplyFogMaterialParameters()
    {
        if (fogMaterial == null)
            return;

        fogMaterial.SetTexture(FogMaskId, fogMaskRT);

        if (fogMaterial.HasProperty(MaskSoftnessId))
            fogMaterial.SetFloat(MaskSoftnessId, RadiusLockedMaskSoftness);

        if (fogMaterial.HasProperty(MaskContrastId))
            fogMaterial.SetFloat(MaskContrastId, materialMaskContrast);
    }

    public void ForceUpdate()
    {
        InitializeOrRefresh();
        RenderFogMask();
    }

    void ReleaseRenderTexture()
    {
        if (fogMaskRT == null)
            return;

        if (renderCamera != null && renderCamera.targetTexture == fogMaskRT)
            renderCamera.targetTexture = null;

        if (RenderTexture.active == fogMaskRT)
            RenderTexture.active = null;

        if (fogMaskRT.IsCreated())
            fogMaskRT.Release();

        if (Application.isPlaying)
            Destroy(fogMaskRT);
        else
            DestroyImmediate(fogMaskRT);

        fogMaskRT = null;
    }

    void OnDisable()
    {
        if (renderCamera != null)
            renderCamera.targetTexture = null;

        ReleaseRenderTexture();

        if (drawMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(drawMaterial);
            else
                DestroyImmediate(drawMaterial);

            drawMaterial = null;
        }
    }

    void OnDestroy()
    {
        OnDisable();
    }
}
