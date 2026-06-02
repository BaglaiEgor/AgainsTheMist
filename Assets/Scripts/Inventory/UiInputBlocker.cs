using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class UiInputBlocker
{
    private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();

    public static bool IsPointerOverBlockingUI()
    {
        if (EventSystem.current == null)
            return false;

        Vector2 pointerPosition = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition
        };

        RaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, RaycastResults);

        for (int i = 0; i < RaycastResults.Count; i++)
        {
            GameObject hitObject = RaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            Graphic graphic = hitObject.GetComponent<Graphic>();
            if (graphic != null)
            {
                if (!graphic.raycastTarget)
                    continue;

                if (graphic.color.a <= 0.001f)
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

public static class WorldInputBlocker
{
    public static bool ShouldBlockWorldInput()
    {
        return UiInputBlocker.IsPointerOverBlockingUI();
    }
}
