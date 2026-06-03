using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryWorldTooltipController : MonoBehaviour
{
    [Header("World Interact Tooltip")]
    [SerializeField] private GameObject worldInteractTooltipRoot;
    [SerializeField] private RectTransform worldInteractTooltipRect;
    [SerializeField] private Image worldInteractTooltipIcon;
    [SerializeField] private TextMeshProUGUI worldInteractTooltipText;
    [SerializeField] private string worldInteractLabel = "Открыть";
    [SerializeField] private Vector2 worldInteractTooltipOffset = new Vector2(20f, 20f);
    [SerializeField] private Transform interactionSource;
    [SerializeField] private bool hideWorldTooltipOverUI = true;

    private readonly List<RaycastResult> uiRaycastResults = new();
    private Canvas canvas;

    public void Initialize(Canvas ownerCanvas, Transform source)
    {
        canvas = ownerCanvas;
        if (source != null)
            interactionSource = source;
        TooltipTextMotion.EnsureOn(worldInteractTooltipText);
        Hide();
    }

    public void Configure(
        GameObject tooltipRoot,
        RectTransform tooltipRect,
        Image tooltipIcon,
        TextMeshProUGUI tooltipText,
        string label,
        Vector2 offset,
        Transform source,
        bool hideOverUi)
    {
        if (tooltipRoot != null)
            worldInteractTooltipRoot = tooltipRoot;
        if (tooltipRect != null)
            worldInteractTooltipRect = tooltipRect;
        if (tooltipIcon != null)
            worldInteractTooltipIcon = tooltipIcon;
        if (tooltipText != null)
            worldInteractTooltipText = tooltipText;
        if (!string.IsNullOrWhiteSpace(label))
            worldInteractLabel = label;

        TooltipTextMotion.EnsureOn(worldInteractTooltipText);

        worldInteractTooltipOffset = offset;
        hideWorldTooltipOverUI = hideOverUi;
        if (source != null)
            interactionSource = source;
    }

    public void Tick()
    {
        if (worldInteractTooltipRoot == null)
            return;

        if (hideWorldTooltipOverUI && IsPointerOverBlockingUI())
        {
            Hide();
            return;
        }

        if (Mouse.current == null || Camera.main == null)
        {
            Hide();
            return;
        }

        if (!TryGetHoveredInteractable(out Component interactable))
        {
            Hide();
            return;
        }

        Show(Mouse.current.position.ReadValue(), GetLabel(interactable));
    }

    public void Hide()
    {
        if (worldInteractTooltipRoot != null && worldInteractTooltipRoot.activeSelf)
            worldInteractTooltipRoot.SetActive(false);
    }

    private bool TryGetHoveredInteractable(out Component interactable)
    {
        interactable = null;
        if (Camera.main == null || Mouse.current == null)
            return false;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);
        if (hits == null || hits.Length == 0)
            return false;

        Component stationCandidate = null;
        BeaconUpgrade beaconCandidate = null;
        DonationFountain fountainCandidate = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            LighthouseKeeperNPC keeper = hit.GetComponentInParent<LighthouseKeeperNPC>();
            if (keeper != null)
            {
                if (interactionSource != null && !keeper.CanInteract(interactionSource))
                    continue;

                interactable = keeper;
                return true;
            }

            ChestInventory chest = hit.GetComponentInParent<ChestInventory>();
            if (chest != null)
            {
                if (interactionSource != null && !chest.CanInteract(interactionSource))
                    continue;

                interactable = chest;
                return true;
            }

            Door door = hit.GetComponentInParent<Door>();
            if (door != null)
            {
                if (interactionSource != null && !door.CanInteract(interactionSource))
                    continue;

                interactable = door;
                return true;
            }

            KeyFence keyFence = hit.GetComponentInParent<KeyFence>();
            if (keyFence != null)
            {
                if (interactionSource != null && !keyFence.CanShowTooltip(interactionSource))
                    continue;

                interactable = keyFence;
                return true;
            }

            EntryAndExit entryAndExit = hit.GetComponentInParent<EntryAndExit>();
            if (entryAndExit != null)
            {
                if (interactionSource != null && !entryAndExit.CanInteract(interactionSource))
                    continue;

                interactable = entryAndExit;
                return true;
            }

            DungeonLightSource lightSource = hit.GetComponentInParent<DungeonLightSource>();
            if (lightSource != null)
            {
                if (interactionSource != null && !lightSource.CanInteract(interactionSource))
                    continue;

                interactable = lightSource;
                return true;
            }

            DungeonLightMirror lightMirror = hit.GetComponentInParent<DungeonLightMirror>();
            if (lightMirror != null)
            {
                if (interactionSource != null && !lightMirror.CanInteract(interactionSource))
                    continue;

                interactable = lightMirror;
                return true;
            }

            CraftingStation station = hit.GetComponentInParent<CraftingStation>();
            if (station == null)
                continue;

            if (interactionSource != null && !station.CanInteract(interactionSource))
                continue;

            if (station.StationType == CraftStationType.Furnace)
            {
                FurnaceStation furnace = station.GetComponent<FurnaceStation>();
                if (furnace != null)
                    stationCandidate = furnace;
            }
            else
            {
                stationCandidate = station;
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            BeaconUpgrade beaconUpgrade = hit.GetComponentInParent<BeaconUpgrade>();
            if (beaconUpgrade != null)
            {
                if (interactionSource != null && !beaconUpgrade.CanInteract(interactionSource))
                    continue;

                beaconCandidate = beaconUpgrade;
                break;
            }

            DonationFountain fountain = hit.GetComponentInParent<DonationFountain>();
            if (fountain != null)
            {
                if (interactionSource != null && !fountain.CanShowTooltip(interactionSource))
                    continue;

                fountainCandidate = fountain;
                break;
            }
        }

        if (stationCandidate != null)
        {
            interactable = stationCandidate;
            return true;
        }

        if (beaconCandidate != null)
        {
            interactable = beaconCandidate;
            return true;
        }

        if (fountainCandidate != null)
        {
            interactable = fountainCandidate;
            return true;
        }

        return false;
    }
    private string GetLabel(Component interactable)
    {
        if (interactable is LighthouseKeeperNPC)
            return "Поговорить";

        if (interactable is Door door)
            return door.IsOpen ? "Закрыть" : "Открыть";

        if (interactable is KeyFence keyFence)
            return keyFence.InteractLabel;

        if (interactable is DonationFountain fountain)
            return fountain.InteractLabel;

        if (interactable is EntryAndExit entryAndExit)
            return entryAndExit.InteractLabel;

        if (interactable is DungeonLightSource lightSource)
            return lightSource.InteractLabel;

        if (interactable is DungeonLightMirror lightMirror)
            return lightMirror.InteractLabel;

        return worldInteractLabel;
    }

    private void Show(Vector2 screenPosition, string label)
    {
        if (worldInteractTooltipText != null)
            worldInteractTooltipText.text = label;

        TooltipTextMotion.EnsureOn(worldInteractTooltipText)?.RefreshBasePosition();

        if (worldInteractTooltipIcon != null)
            worldInteractTooltipIcon.enabled = worldInteractTooltipIcon.sprite != null;

        if (!worldInteractTooltipRoot.activeSelf)
            worldInteractTooltipRoot.SetActive(true);

        Position(screenPosition);
    }

    private void Position(Vector2 screenPosition)
    {
        if (worldInteractTooltipRoot == null)
            return;

        RectTransform rootRect = worldInteractTooltipRoot.transform as RectTransform;
        RectTransform tooltipRect = rootRect;
        if (worldInteractTooltipRect != null &&
            rootRect != null &&
            worldInteractTooltipRect != rootRect &&
            worldInteractTooltipRect.IsChildOf(rootRect))
        {
            tooltipRect = worldInteractTooltipRect;
        }

        if (tooltipRect == null || canvas == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint);

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 pivotFromBottomLeft = new Vector2(
            tooltipRect.rect.width * tooltipRect.pivot.x,
            tooltipRect.rect.height * tooltipRect.pivot.y
        );

        tooltipRect.anchoredPosition = localPoint + (worldInteractTooltipOffset / scale) + pivotFromBottomLeft;
    }

    private bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            if (worldInteractTooltipRoot != null)
            {
                Transform tooltipTransform = worldInteractTooltipRoot.transform;
                Transform hitTransform = hitObject.transform;
                if (hitTransform == tooltipTransform || hitTransform.IsChildOf(tooltipTransform))
                    continue;
            }

            Graphic graphic = hitObject.GetComponent<Graphic>();
            if (graphic != null)
            {
                if (!graphic.raycastTarget || graphic.color.a <= 0.001f)
                    continue;
            }

            CanvasGroup canvasGroup = hitObject.GetComponentInParent<CanvasGroup>();
            if (canvasGroup != null && (!canvasGroup.blocksRaycasts || canvasGroup.alpha <= 0.001f))
                continue;

            return true;
        }

        return false;
    }
}
