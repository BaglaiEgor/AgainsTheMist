using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingCategoryGroupUI : MonoBehaviour
{
    [SerializeField] private Button headerButton;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private RectTransform recipesContainer;

    private CraftingCategory category;

    public Transform RecipesParent => recipesContainer != null ? recipesContainer : transform;

    public void Setup(CraftingCategory category)
    {
        this.category = category;

        if (headerButton != null)
            headerButton.onClick.RemoveListener(ToggleExpanded);

        if (recipesContainer != null)
            recipesContainer.gameObject.SetActive(true);

        UpdateHeaderText();
    }

    private static string GetCategoryDisplayName(CraftingCategory category)
    {
        switch (category)
        {
            case CraftingCategory.All:
                return "\u0412\u0441\u0435";
            case CraftingCategory.Basic:
                return "\u0411\u0430\u0437\u043E\u0432\u044B\u0435";
            case CraftingCategory.Tools:
                return "\u0418\u043D\u0441\u0442\u0440\u0443\u043C\u0435\u043D\u0442\u044B";
            case CraftingCategory.Equipment:
                return "\u042D\u043A\u0438\u043F\u0438\u0440\u043E\u0432\u043A\u0430";
            case CraftingCategory.Resources:
                return "\u0420\u0435\u0441\u0443\u0440\u0441\u044B";
            case CraftingCategory.Furniture:
                return "\u0421\u0442\u0440\u0443\u043A\u0442\u0443\u0440\u044B";
            case CraftingCategory.Survival:
                return "\u0412\u044B\u0436\u0438\u0432\u0430\u043D\u0438\u0435";
            case CraftingCategory.Consumables:
                return "\u0420\u0430\u0441\u0445\u043E\u0434\u043D\u0438\u043A\u0438";
            case CraftingCategory.Weapons:
                return "\u041E\u0440\u0443\u0436\u0438\u0435";
            case CraftingCategory.Others:
                return "\u041F\u0440\u043E\u0447\u0435\u0435";
            default:
                return category.ToString();
        }
    }

    private void OnDestroy()
    {
        if (headerButton != null)
            headerButton.onClick.RemoveListener(ToggleExpanded);
    }

    private void ToggleExpanded()
    {
    }

    private void UpdateHeaderText()
    {
        if (headerText == null)
            return;

        headerText.text = GetCategoryDisplayName(category);
    }
}
