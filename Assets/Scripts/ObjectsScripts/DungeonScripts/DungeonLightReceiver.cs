using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DungeonLightReceiver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Colors")]
    [SerializeField] private Color unlitColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color litColor = new Color(1f, 0.9f, 0.25f, 1f);

    private int lastLightFrame = -1;
    private bool isLit;

    public bool IsLit => isLit;
    public event Action<DungeonLightReceiver> StateChanged;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        RefreshVisual();
    }

    private void LateUpdate()
    {
        if (isLit && lastLightFrame != Time.frameCount)
            SetLit(false);
    }

    public void ReceiveLight()
    {
        lastLightFrame = Time.frameCount;
        SetLit(true);
    }

    private void SetLit(bool lit)
    {
        if (isLit == lit)
            return;

        isLit = lit;
        RefreshVisual();
        StateChanged?.Invoke(this);
    }

    private void RefreshVisual()
    {
        if (spriteRenderer != null)
            spriteRenderer.color = isLit ? litColor : unlitColor;
    }
}
