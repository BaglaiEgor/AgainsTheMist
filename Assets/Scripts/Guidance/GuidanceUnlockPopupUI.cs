using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GuidanceUnlockPopupUI : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 42f);
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private string unlockMessage = "Подсказка разблокирована";

    private Canvas canvas;
    private Camera worldCamera;
    private Vector3 worldPosition;
    private float hideTime;
    private Sprite defaultIcon;

    private void Awake()
    {
        if (root == null)
            root = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (iconImage != null)
            defaultIcon = iconImage.sprite;
    }

    private void Update()
    {
        UpdatePosition();

        if (Time.time >= hideTime)
            Destroy(gameObject);
    }

    public void Show(GuidanceEntry entry, Vector3 targetWorldPosition, Canvas targetCanvas)
    {
        canvas = targetCanvas;
        worldCamera = Camera.main;
        worldPosition = targetWorldPosition;
        hideTime = Time.time + Mathf.Max(0.1f, lifetime);

        if (messageText != null)
            messageText.text = unlockMessage;

        if (iconImage != null)
        {
            iconImage.sprite = entry != null && entry.Icon != null ? entry.Icon : defaultIcon;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (canvas == null || root == null || worldCamera == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
        screenPosition += screenOffset;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            root.anchoredPosition = localPoint;
    }
}
