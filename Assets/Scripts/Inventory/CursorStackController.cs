using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CursorStackController : MonoBehaviour, IItemContainer
{
    private static int activeCursorCount;

    [Header("Visual")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(14f, -14f);

    private readonly InventoryItem cursorSlot = new InventoryItem();
    private int transactionDepth;
    private bool wasActiveBeforeTransaction;
    private bool countedAsActive;

    private RectTransform dragParent;
    private Canvas rootCanvas;
    private RectTransform root;
    private Image background;
    private Image icon;
    private TextMeshProUGUI amountText;

    public static bool HasAnyActiveCursor => activeCursorCount > 0;
    public bool HasActiveIntent => !cursorSlot.IsEmpty && cursorSlot.item != null && cursorSlot.amount > 0;
    public string ContainerName => "Cursor";
    public int SlotCount => 1;
    public Transform ContainerTransform => transform;

    public void Initialize(Transform dragParentTransform)
    {
        dragParent = dragParentTransform as RectTransform;
        ResolveRootCanvas(dragParentTransform);
        EnsureVisual();
        HideVisual();
    }

    private void Update()
    {
        RefreshActiveCount();
        if (!HasActiveIntent)
        {
            HideVisual();
            return;
        }

        UpdateVisual(cursorSlot.item, cursorSlot.amount);
        FollowPointer();
    }

    private void OnDisable()
    {
        if (countedAsActive)
        {
            activeCursorCount = Mathf.Max(0, activeCursorCount - 1);
            countedAsActive = false;
        }

        HideVisual();
    }

    public bool TryBeginIntent(IItemContainer container, int slotIndex, int requestedAmount)
    {
        if (HasActiveIntent)
            return false;

        if (container == null || !container.IsValidSlot(slotIndex) || requestedAmount <= 0)
            return false;

        InventoryItem slot = container.GetItem(slotIndex);
        if (slot == null || slot.IsEmpty || slot.item == null || slot.amount <= 0)
            return false;
        if (!container.CanExtract(slotIndex))
            return false;

        int amountToMove = Mathf.Clamp(requestedAmount, 1, slot.amount);
        if (!InventoryTransactionService.TryMove(container, slotIndex, this, 0, amountToMove))
            return false;

        RefreshActiveCount();
        UpdateVisual(cursorSlot.item, cursorSlot.amount);
        FollowPointer();
        return true;
    }

    public bool TryGetIntent(out IItemContainer container, out int slotIndex, out int requestedAmount, out ItemData item)
    {
        container = this;
        slotIndex = 0;
        return TryGetStack(out item, out requestedAmount);
    }

    public bool IsSourceSlot(IItemContainer container, int slotIndex)
    {
        return false;
    }

    public bool TryGetStack(out ItemData item, out int amount)
    {
        item = cursorSlot.item;
        amount = cursorSlot.amount;
        return HasActiveIntent;
    }

    public bool TryConsume(int amount)
    {
        if (amount <= 0)
            return false;

        if (!TryGetStack(out ItemData item, out int currentAmount))
            return false;

        if (amount > currentAmount)
            return false;

        SetSlotFromTransaction(0, item, currentAmount - amount);
        return true;
    }

    public bool TryReturnToContainer(IItemContainer target)
    {
        if (!HasActiveIntent || target == null)
            return true;

        return InventoryTransactionService.TryMoveToContainer(this, 0, target, cursorSlot.amount);
    }

    public InventoryItem GetItem(int slotIndex)
    {
        return IsValidSlot(slotIndex) ? cursorSlot : null;
    }

    public bool IsValidSlot(int slotIndex)
    {
        return slotIndex == 0;
    }

    public bool CanAccept(int slotIndex, ItemData item)
    {
        return IsValidSlot(slotIndex) && item != null;
    }

    public bool CanExtract(int slotIndex)
    {
        return IsValidSlot(slotIndex) && HasActiveIntent;
    }

    public void BeginTransaction()
    {
        if (transactionDepth == 0)
            wasActiveBeforeTransaction = HasActiveIntent;

        transactionDepth++;
    }

    public void EndTransaction()
    {
        transactionDepth = Mathf.Max(0, transactionDepth - 1);
        if (transactionDepth > 0)
            return;

        bool isActiveAfterTransaction = HasActiveIntent;
        if (wasActiveBeforeTransaction != isActiveAfterTransaction)
            RefreshActiveCount();

        wasActiveBeforeTransaction = isActiveAfterTransaction;
        if (isActiveAfterTransaction)
            UpdateVisual(cursorSlot.item, cursorSlot.amount);
        else
            HideVisual();
    }

    public void SetSlotFromTransaction(int slotIndex, ItemData item, int amount)
    {
        if (!IsValidSlot(slotIndex))
            return;

        bool wasActive = HasActiveIntent;
        if (item == null || amount <= 0)
        {
            cursorSlot.Clear();
        }
        else
        {
            cursorSlot.item = item;
            cursorSlot.amount = Mathf.Clamp(amount, 1, Mathf.Max(1, item.maxStack));
        }

        if (transactionDepth <= 0 && wasActive != HasActiveIntent)
            RefreshActiveCount();

        RefreshVisualNow();
    }

    private void ResolveRootCanvas(Transform dragParentTransform)
    {
        Canvas canvas = null;
        if (dragParentTransform != null)
            canvas = dragParentTransform.GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    private void EnsureVisual()
    {
        if (root != null)
            return;

        if (rootCanvas == null)
            ResolveRootCanvas(dragParent);

        Transform parent = rootCanvas != null ? rootCanvas.transform : (dragParent != null ? dragParent : transform);
        GameObject rootObject = new GameObject("CursorStack", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootObject.transform.SetParent(parent, false);
        rootObject.transform.SetAsLastSibling();

        root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0f, 1f);
        root.sizeDelta = new Vector2(56f, 56f);

        CanvasGroup canvasGroup = rootObject.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        background = rootObject.GetComponent<Image>();
        background.raycastTarget = false;
        background.color = new Color(1f, 1f, 1f, 0.45f);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Outline));
        iconObject.transform.SetParent(rootObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(4f, 4f);
        iconRect.offsetMax = new Vector2(-4f, -4f);

        icon = iconObject.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.color = new Color(1f, 1f, 1f, 0.95f);

        Outline iconOutline = iconObject.GetComponent<Outline>();
        iconOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        iconOutline.effectDistance = new Vector2(1f, -1f);

        GameObject textObject = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rootObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(2f, 2f);
        textRect.offsetMax = new Vector2(-4f, -2f);

        amountText = textObject.GetComponent<TextMeshProUGUI>();
        amountText.raycastTarget = false;
        amountText.fontSize = 16f;
        amountText.alignment = TextAlignmentOptions.BottomRight;
        amountText.color = Color.black;
    }

    private void UpdateVisual(ItemData item, int visualAmount)
    {
        EnsureVisual();
        if (root == null)
            return;

        root.SetParent(rootCanvas != null ? rootCanvas.transform : root.parent, false);
        root.SetAsLastSibling();
        root.gameObject.SetActive(true);

        if (icon != null)
        {
            icon.sprite = item != null ? item.icon : null;
            icon.enabled = icon.sprite != null;
        }

        if (amountText != null)
            amountText.text = visualAmount > 0 ? visualAmount.ToString() : string.Empty;
    }

    private void HideVisual()
    {
        if (root != null)
            root.gameObject.SetActive(false);
    }

    private void FollowPointer()
    {
        if (root == null)
            return;

        if (rootCanvas == null)
            ResolveRootCanvas(dragParent);

        Vector2 pointerPosition = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        RectTransform parentRect = rootCanvas != null
            ? rootCanvas.transform as RectTransform
            : root.parent as RectTransform;

        if (parentRect == null)
        {
            Vector2 fallbackPivotFromBottomLeft = new Vector2(
                root.rect.width * root.pivot.x,
                root.rect.height * root.pivot.y
            );
            root.position = pointerPosition + cursorOffset + fallbackPivotFromBottomLeft;
            return;
        }

        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, pointerPosition, uiCamera, out Vector2 localPoint);
        float scale = rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
        Vector2 pivotFromBottomLeft = new Vector2(
            root.rect.width * root.pivot.x,
            root.rect.height * root.pivot.y
        );

        root.anchoredPosition = localPoint + (cursorOffset / scale) + pivotFromBottomLeft;
    }

    private void RefreshVisualNow()
    {
        if (!HasActiveIntent)
        {
            HideVisual();
            return;
        }

        UpdateVisual(cursorSlot.item, cursorSlot.amount);
        FollowPointer();
    }

    private void RefreshActiveCount()
    {
        bool shouldBeActive = HasActiveIntent;

        if (shouldBeActive == countedAsActive)
            return;

        if (shouldBeActive)
            activeCursorCount++;
        else
            activeCursorCount = Mathf.Max(0, activeCursorCount - 1);

        countedAsActive = shouldBeActive;
    }
}
