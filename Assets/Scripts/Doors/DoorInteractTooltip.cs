using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class DoorInteractTooltip : MonoBehaviour
{
    private const string TooltipResourcePath = "UI/InteractTooltip";

    [SerializeField] private Door door;
    [SerializeField] private GameObject tooltipPrefab;
    [SerializeField] private Vector2 offset = new Vector2(18f, 18f);

    private GameObject tooltipInstance;
    private RectTransform tooltipRect;
    private TextMeshProUGUI tooltipText;
    private Canvas canvas;
    private bool isHovering;

    void Awake()
    {
        if (door == null)
            door = GetComponentInParent<Door>();
    }

    void OnDisable()
    {
        Hide();
    }

    void Update()
    {
        if (!isHovering || tooltipInstance == null || !tooltipInstance.activeSelf)
            return;

        UpdateText();
        FollowCursor();
    }

    void OnMouseEnter()
    {
        Show();
    }

    void OnMouseExit()
    {
        Hide();
    }

    void Show()
    {
        EnsureTooltip();
        if (tooltipInstance == null)
            return;

        isHovering = true;
        UpdateText();
        tooltipInstance.SetActive(true);
        FollowCursor();
    }

    void Hide()
    {
        isHovering = false;

        if (tooltipInstance != null)
            tooltipInstance.SetActive(false);
    }

    void EnsureTooltip()
    {
        if (tooltipInstance != null)
            return;

        canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        GameObject prefab = tooltipPrefab != null ? tooltipPrefab : Resources.Load<GameObject>(TooltipResourcePath);
        if (prefab == null)
            return;

        tooltipInstance = Instantiate(prefab, canvas.transform);
        tooltipRect = tooltipInstance.GetComponent<RectTransform>();
        tooltipText = tooltipInstance.GetComponentInChildren<TextMeshProUGUI>(true);
        TooltipTextMotion.EnsureOn(tooltipText);
        tooltipInstance.SetActive(false);
    }

    void UpdateText()
    {
        if (tooltipText == null)
            return;

        tooltipText.text = door != null && door.IsOpen ? "Закрыть" : "Открыть";
        TooltipTextMotion.EnsureOn(tooltipText)?.RefreshBasePosition();
    }

    void FollowCursor()
    {
        if (tooltipRect == null || canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 screenPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out Vector2 localPosition
        );

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        tooltipRect.anchoredPosition = localPosition + offset / scale;
    }
}
