using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DungeonLightReceiver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite unlitSprite;
    [SerializeField] private Sprite litSprite;

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
        if (spriteRenderer == null)
            return;

        Sprite targetSprite = isLit ? litSprite : unlitSprite;
        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
    }
}
