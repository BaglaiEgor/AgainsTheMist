using UnityEngine;

public class TabManager : MonoBehaviour
{
    public GameObject inventoryPanel;
    public GameObject craftPanel;

    public void ShowInventory() => inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    public void ShowCraft() => craftPanel.SetActive(!craftPanel.activeSelf);
}
