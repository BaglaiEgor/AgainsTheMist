using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDropShadow : MonoBehaviour
{
    [Header("Visual")]
    [Range(0f, 1f)] [SerializeField] private float opacity = 0.28f;
    [SerializeField] private Vector2 size = new Vector2(0.8f, 0.22f);
    [SerializeField] private Vector3 localOffset = new Vector3(0f, -0.36f, 0.01f);
    [SerializeField] private int sortingOrderOffset = -1;

    private SpriteRenderer playerRenderer;
    private SpriteRenderer shadowRenderer;
    private Texture2D shadowTexture;

    private void Awake()
    {
        playerRenderer = GetComponentInChildren<SpriteRenderer>();
        CreateShadow();
    }

    private void LateUpdate()
    {
        if (shadowRenderer == null)
            return;

        if (playerRenderer == null)
            playerRenderer = GetComponentInChildren<SpriteRenderer>();

        shadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Clamp01(opacity));
        shadowRenderer.transform.localPosition = localOffset;
        shadowRenderer.transform.localScale = new Vector3(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y * 2f),
            1f
        );

        if (playerRenderer == null)
            return;

        shadowRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = playerRenderer.sortingOrder + sortingOrderOffset;
    }

    private void CreateShadow()
    {
        GameObject shadow = new GameObject("Drop Shadow");
        shadow.transform.SetParent(transform, false);

        shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = CreateShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, opacity);
    }

    private Sprite CreateShadowSprite()
    {
        const int width = 64;
        const int height = 32;

        shadowTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedX = (x + 0.5f) / width * 2f - 1f;
                float normalizedY = (y + 0.5f) / height * 2f - 1f;
                float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;

                shadowTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        shadowTexture.Apply();
        return Sprite.Create(shadowTexture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
    }

    private void OnDestroy()
    {
        if (shadowTexture != null)
            Destroy(shadowTexture);
    }
}
