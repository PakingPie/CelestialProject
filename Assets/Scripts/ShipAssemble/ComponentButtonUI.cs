using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComponentButtonUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public Image selectionHighlight;
    
    private ShipComponentData componentData;
    
    public void Setup(ShipComponentData data)
    {
        componentData = data;
        
        if (nameText != null)
            nameText.text = data.ComponentName;
        
        if (iconImage != null && data.UiIcon != null)
            iconImage.sprite = data.UiIcon;
        
        if (selectionHighlight != null)
            selectionHighlight.gameObject.SetActive(false);
    }
    
    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.gameObject.SetActive(selected);
    }
}