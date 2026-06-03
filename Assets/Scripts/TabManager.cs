using UnityEngine;

public class TabManager : MonoBehaviour
{
    public GameObject inventoryPanel;
    public GameObject craftPanel;

    public void ShowInventory() => inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    public void ShowCraft() => craftPanel.SetActive(!craftPanel.activeSelf);

    public bool CloseOpenedPanels()
    {
        bool closedAny = false;

        if (inventoryPanel != null && inventoryPanel.activeSelf)
        {
            inventoryPanel.SetActive(false);
            closedAny = true;
        }

        if (craftPanel != null && craftPanel.activeSelf)
        {
            craftPanel.SetActive(false);
            closedAny = true;
        }

        return closedAny;
    }
}
