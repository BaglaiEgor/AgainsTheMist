using UnityEngine;
using UnityEngine.UI;

public class ObjectHealthUI : MonoBehaviour
{
    [SerializeField] private Image healthBar;
    [SerializeField] private GameObject hpBarRoot;

    public void SetHealth(float current, float max)
    {
        healthBar.fillAmount = (float)current / (float)max;

        hpBarRoot.SetActive(current < max);
    }
}
