using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class ChestOpenVisual : MonoBehaviour
{
    [SerializeField] private ChestInventory chestInventory;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openedSprite;
    [SerializeField] private Sprite[] openFrames;
    [Min(0.01f)] [SerializeField] private float frameDuration = 0.08f;

    private Coroutine openRoutine;

    void Awake()
    {
        if (chestInventory == null)
            chestInventory = GetComponentInChildren<ChestInventory>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (chestInventory != null)
            chestInventory.OnChestOpenStateChanged += HandleOpenStateChanged;
    }

    void Start()
    {
        if (!Application.isPlaying)
            return;

        ApplyInstant(chestInventory != null && chestInventory.IsOpen);
    }

    void OnDisable()
    {
        if (chestInventory != null)
            chestInventory.OnChestOpenStateChanged -= HandleOpenStateChanged;
    }

    private void HandleOpenStateChanged(bool opened)
    {
        if (opened)
            PlayOpenAnimation();
        else
            ApplyInstant(false);
    }

    private void PlayOpenAnimation()
    {
        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(PlayOpenRoutine());
    }

    private IEnumerator PlayOpenRoutine()
    {
        if (spriteRenderer == null || openFrames == null || openFrames.Length == 0)
        {
            ApplyInstant(true);
            yield break;
        }

        for (int i = 0; i < openFrames.Length; i++)
        {
            if (openFrames[i] != null)
                spriteRenderer.sprite = openFrames[i];

            yield return new WaitForSeconds(frameDuration);
        }

        if (openedSprite != null)
            spriteRenderer.sprite = openedSprite;

        openRoutine = null;
    }

    private void ApplyInstant(bool opened)
    {
        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
            openRoutine = null;
        }

        if (spriteRenderer == null)
            return;

        if (opened && openedSprite != null)
            spriteRenderer.sprite = openedSprite;
        else if (!opened && closedSprite != null)
            spriteRenderer.sprite = closedSprite;
    }
}
