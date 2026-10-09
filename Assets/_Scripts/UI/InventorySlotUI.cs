using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private GameObject root;

    private bool hasWarnedAboutMissingIcon;

    public void SetSlot(Sprite icon, string itemName, string slotAmountText)
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.color = icon != null ? Color.white : new Color(0.18f, 0.2f, 0.22f, 0.95f);
        }

        if (nameText != null)
        {
            nameText.text = itemName;
        }

        if (amountText != null)
        {
            amountText.text = slotAmountText;
        }

        if (icon == null && !hasWarnedAboutMissingIcon)
        {
            Debug.LogWarning($"InventorySlotUI '{name}': missing icon for {itemName}. Assign a Sprite in InventoryUI.", this);
            hasWarnedAboutMissingIcon = true;
        }
    }

    public void SetVisible(bool visible)
    {
        GameObject target = root != null ? root : gameObject;
        target.SetActive(visible);
    }

    public void Bind(Image icon, TextMeshProUGUI itemName, TextMeshProUGUI amount, GameObject rootObject = null)
    {
        iconImage = icon;
        nameText = itemName;
        amountText = amount;
        root = rootObject != null ? rootObject : gameObject;
    }
}
