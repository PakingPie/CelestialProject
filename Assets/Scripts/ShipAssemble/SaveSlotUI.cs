using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SaveSlotUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI shipNameText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI statsText;
    public RawImage thumbnailImage;
    public Button loadButton;
    public Button deleteButton;
    
    private ShipSaveInfo saveInfo;
    private Action<ShipSaveInfo> onLoad;
    private Action<ShipSaveInfo> onDelete;
    private Texture2D loadedThumbnail;
    
    public void Setup(ShipSaveInfo info, Action<ShipSaveInfo> loadCallback, Action<ShipSaveInfo> deleteCallback)
    {
        saveInfo = info;
        onLoad = loadCallback;
        onDelete = deleteCallback;
        
        // Set text
        if (shipNameText != null)
            shipNameText.text = info.shipName;
        
        if (dateText != null)
            dateText.text = info.saveDate;
        
        if (statsText != null)
            statsText.text = $"Bodies: {info.bodyCount} | Hull: {info.totalHull:F0} | Weight: {info.totalWeight:F1}";
        
        // Load thumbnail
        if (thumbnailImage != null)
        {
            loadedThumbnail = info.GetThumbnail();
            if (loadedThumbnail != null)
            {
                thumbnailImage.texture = loadedThumbnail;
                thumbnailImage.gameObject.SetActive(true);
            }
            else
            {
                thumbnailImage.gameObject.SetActive(false);
            }
        }
        
        // Setup buttons
        loadButton?.onClick.AddListener(OnLoadClicked);
        deleteButton?.onClick.AddListener(OnDeleteClicked);
    }
    
    private void OnLoadClicked()
    {
        onLoad?.Invoke(saveInfo);
    }
    
    private void OnDeleteClicked()
    {
        onDelete?.Invoke(saveInfo);
    }
    
    void OnDestroy()
    {
        // Clean up thumbnail texture
        if (loadedThumbnail != null)
        {
            Destroy(loadedThumbnail);
        }
    }
}