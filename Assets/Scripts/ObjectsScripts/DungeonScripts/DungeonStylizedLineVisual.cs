using UnityEngine;

[DisallowMultipleComponent]
public class DungeonStylizedLineVisual : MonoBehaviour
{
    private LineRenderer outerLine;
    private LineRenderer coreLine;
    private Material outerMaterial;
    private Material coreMaterial;

    private float outerWidth = 0.22f;
    private float coreWidth = 0.08f;
    private float pulseSpeed = 10f;
    private Color warningOuterColor = new Color(0.55f, 0.12f, 0.82f, 0.55f);
    private Color warningCoreColor = new Color(0.95f, 0.55f, 1f, 0.75f);
    private Color activeOuterColor = new Color(1f, 0.2f, 0.95f, 0.95f);
    private Color activeCoreColor = new Color(1f, 0.85f, 1f, 1f);
    private bool keepWidthStable;

    public void Configure(
        float newOuterWidth,
        float newCoreWidth,
        float newPulseSpeed,
        Color newWarningOuterColor,
        Color newWarningCoreColor,
        Color newActiveOuterColor,
        Color newActiveCoreColor)
    {
        outerWidth = Mathf.Max(0.01f, newOuterWidth);
        coreWidth = Mathf.Max(0.005f, newCoreWidth);
        pulseSpeed = Mathf.Max(0.1f, newPulseSpeed);
        warningOuterColor = newWarningOuterColor;
        warningCoreColor = newWarningCoreColor;
        activeOuterColor = newActiveOuterColor;
        activeCoreColor = newActiveCoreColor;

        EnsureLines();
    }

    public void ApplySorting(SpriteRenderer sourceRenderer, int sortingOrderOffset)
    {
        EnsureLines();

        if (sourceRenderer != null)
        {
            outerLine.sortingLayerID = sourceRenderer.sortingLayerID;
            coreLine.sortingLayerID = sourceRenderer.sortingLayerID;
            outerLine.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
            coreLine.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset + 1;
            return;
        }

        outerLine.sortingOrder = 10 + sortingOrderOffset;
        coreLine.sortingOrder = 11 + sortingOrderOffset;
    }

    public void Show(Vector3 start, Vector3 end, float progress, bool active)
    {
        EnsureLines();

        outerLine.enabled = true;
        coreLine.enabled = true;

        start.z = transform.position.z;
        end.z = transform.position.z;
        outerLine.SetPosition(0, start);
        outerLine.SetPosition(1, end);
        coreLine.SetPosition(0, start);
        coreLine.SetPosition(1, end);

        if (active)
            ApplyActiveStyle(progress);
        else
            ApplyWarningStyle(progress);
    }

    public void Hide()
    {
        if (outerLine != null)
            outerLine.enabled = false;

        if (coreLine != null)
            coreLine.enabled = false;
    }

    private void EnsureLines()
    {
        if (outerLine == null)
            outerLine = CreateLine("Outer", outerWidth, warningOuterColor, out outerMaterial);

        if (coreLine == null)
            coreLine = CreateLine("Core", coreWidth, warningCoreColor, out coreMaterial);
    }

    private LineRenderer CreateLine(string objectName, float width, Color color, out Material material)
    {
        Transform existing = transform.Find(objectName);
        GameObject lineObject = existing != null ? existing.gameObject : new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        if (line == null)
            line = lineObject.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.startWidth = width;
        line.endWidth = width * 0.75f;
        line.startColor = color;
        line.endColor = color;
        line.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        material = shader != null ? new Material(shader) : null;
        if (material != null)
            line.material = material;

        return line;
    }

    public void SetKeepWidthStable(bool value)
    {
        keepWidthStable = value;
    }

    private void ApplyWarningStyle(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        float pulse = 0.65f + Mathf.Sin(Time.time * pulseSpeed) * 0.35f;
        float ramp = Mathf.Lerp(0.5f, 1f, clampedProgress);
        float widthPulse = keepWidthStable ? 1f : pulse;
        float currentOuterWidth = keepWidthStable
            ? outerWidth
            : outerWidth * Mathf.Lerp(0.75f, 1.1f, clampedProgress) * widthPulse;
        float currentCoreWidth = keepWidthStable
            ? coreWidth
            : coreWidth * Mathf.Lerp(0.8f, 1.2f, clampedProgress);

        outerLine.startWidth = currentOuterWidth;
        outerLine.endWidth = keepWidthStable ? currentOuterWidth : currentOuterWidth * 0.7f;
        coreLine.startWidth = currentCoreWidth;
        coreLine.endWidth = keepWidthStable ? currentCoreWidth : currentCoreWidth * 0.65f;

        Color outer = warningOuterColor;
        outer.a = warningOuterColor.a * pulse * ramp;
        Color core = warningCoreColor;
        core.a = warningCoreColor.a * pulse * ramp;

        outerLine.startColor = outer;
        outerLine.endColor = outer * 0.65f;
        coreLine.startColor = core;
        coreLine.endColor = core * 0.55f;
    }

    private void ApplyActiveStyle(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        float flash = 1f - clampedProgress;
        float currentOuterWidth = keepWidthStable
            ? outerWidth
            : Mathf.Lerp(outerWidth * 1.35f, outerWidth * 0.8f, clampedProgress);
        float currentCoreWidth = keepWidthStable
            ? coreWidth
            : Mathf.Lerp(coreWidth * 1.5f, coreWidth * 0.7f, clampedProgress);

        outerLine.startWidth = currentOuterWidth;
        outerLine.endWidth = keepWidthStable ? currentOuterWidth : currentOuterWidth * 0.75f;
        coreLine.startWidth = currentCoreWidth;
        coreLine.endWidth = keepWidthStable ? currentCoreWidth : currentCoreWidth * 0.7f;

        Color outer = activeOuterColor;
        outer.a = Mathf.Lerp(activeOuterColor.a * 0.35f, activeOuterColor.a, flash);
        Color core = activeCoreColor;
        core.a = Mathf.Lerp(activeCoreColor.a * 0.45f, activeCoreColor.a, flash);

        outerLine.startColor = outer;
        outerLine.endColor = outer * 0.7f;
        coreLine.startColor = core;
        coreLine.endColor = core * 0.75f;
    }

    private void OnDestroy()
    {
        if (outerMaterial != null)
            Destroy(outerMaterial);

        if (coreMaterial != null)
            Destroy(coreMaterial);
    }
}
