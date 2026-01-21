using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShipSaveLoadUI : MonoBehaviour
{
    [Header("References")]
    public ShipSaveLoadManager saveLoadManager;
    
    [Header("Save Panel")]
    public GameObject savePanel;
    public TMP_InputField shipNameInput;
    public Button saveButton;
    public Button closeSaveButton;
    public TextMeshProUGUI saveStatusText;
    
    [Header("Load Panel")]
    public GameObject loadPanel;
    public Transform saveListContainer;
    public GameObject saveSlotPrefab;
    public Button closeLoadButton;
    public TextMeshProUGUI loadStatusText;
    
    [Header("Confirm Dialog")]
    public GameObject confirmDialog;
    public TextMeshProUGUI confirmText;
    public Button confirmYesButton;
    public Button confirmNoButton;
    
    [Header("Main Buttons")]
    public Button openSaveButton;
    public Button openLoadButton;
    
    private List<GameObject> spawnedSlots = new List<GameObject>();
    private System.Action pendingConfirmAction;
    
    void Start()
    {
        SetupButtons();
        SetupEvents();
        
        // Hide panels initially
        savePanel.SetActive(false);
        loadPanel.SetActive(false);
        confirmDialog.SetActive(false);
    }
    
    private void SetupButtons()
    {
        openSaveButton.onClick.AddListener(OpenSavePanel);
        openLoadButton.onClick.AddListener(OpenLoadPanel);
        
        saveButton.onClick.AddListener(OnSaveClicked);
        closeSaveButton.onClick.AddListener(() => savePanel.SetActive(false));
        closeLoadButton.onClick.AddListener(() => loadPanel.SetActive(false));
        
        confirmYesButton.onClick.AddListener(OnConfirmYes);
        confirmNoButton.onClick.AddListener(OnConfirmNo);
    }
    
    private void SetupEvents()
    {
        saveLoadManager.OnSaveComplete += OnSaveComplete;
        saveLoadManager.OnLoadComplete += OnLoadComplete;
        saveLoadManager.OnError += OnError;
    }
    
    public void OpenSavePanel()
    {
        savePanel.SetActive(true);
        loadPanel.SetActive(false);
        saveStatusText.text = "";
        shipNameInput.text = "";
        shipNameInput.Select();
    }
    
    public void OpenLoadPanel()
    {
        loadPanel.SetActive(true);
        savePanel.SetActive(false);
        loadStatusText.text = "";
        
        RefreshSaveList();
    }
    
    private void OnSaveClicked()
    {
        string shipName = shipNameInput.text.Trim();
        
        if (string.IsNullOrEmpty(shipName))
        {
            saveStatusText.text = "Please enter a ship name";
            return;
        }
        
        // Check if save already exists
        if (saveLoadManager.SaveExists(shipName))
        {
            ShowConfirmDialog(
                $"A save named '{shipName}' already exists. Overwrite?",
                () => saveLoadManager.SaveShip(shipName)
            );
        }
        else
        {
            saveLoadManager.SaveShip(shipName);
        }
    }
    
    private void RefreshSaveList()
    {
        // Clear existing slots
        foreach (var slot in spawnedSlots)
        {
            Destroy(slot);
        }
        spawnedSlots.Clear();
        
        // Get saved ships
        List<ShipSaveInfo> saves = saveLoadManager.GetSavedShips();
        
        if (saves.Count == 0)
        {
            loadStatusText.text = "No saved ships found";
            return;
        }
        
        loadStatusText.text = "";
        
        // Create slots
        foreach (var save in saves)
        {
            CreateSaveSlot(save);
        }
    }
    
    private void CreateSaveSlot(ShipSaveInfo saveInfo)
    {
        GameObject slot = Instantiate(saveSlotPrefab, saveListContainer);
        spawnedSlots.Add(slot);
        
        SaveSlotUI slotUI = slot.GetComponent<SaveSlotUI>();
        
        if (slotUI != null)
        {
            slotUI.Setup(saveInfo, OnLoadSlot, OnDeleteSlot);
        }
        else
        {
            // Fallback setup
            var nameText = slot.GetComponentInChildren<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = $"{saveInfo.shipName}\n{saveInfo.saveDate}\nHull: {saveInfo.totalHull:F0}";
            }
            
            var loadBtn = slot.transform.Find("LoadButton")?.GetComponent<Button>();
            loadBtn?.onClick.AddListener(() => OnLoadSlot(saveInfo));
            
            var deleteBtn = slot.transform.Find("DeleteButton")?.GetComponent<Button>();
            deleteBtn?.onClick.AddListener(() => OnDeleteSlot(saveInfo));
        }
    }
    
    private void OnLoadSlot(ShipSaveInfo saveInfo)
    {
        ShowConfirmDialog(
            $"Load ship '{saveInfo.shipName}'? Current build will be lost.",
            () =>
            {
                saveLoadManager.LoadShip(saveInfo.shipName);
                loadPanel.SetActive(false);
            }
        );
    }
    
    private void OnDeleteSlot(ShipSaveInfo saveInfo)
    {
        ShowConfirmDialog(
            $"Delete save '{saveInfo.shipName}'? This cannot be undone.",
            () =>
            {
                saveLoadManager.DeleteShip(saveInfo.shipName);
                RefreshSaveList();
            }
        );
    }
    
    private void ShowConfirmDialog(string message, System.Action onConfirm)
    {
        confirmText.text = message;
        pendingConfirmAction = onConfirm;
        confirmDialog.SetActive(true);
    }
    
    private void OnConfirmYes()
    {
        confirmDialog.SetActive(false);
        pendingConfirmAction?.Invoke();
        pendingConfirmAction = null;
    }
    
    private void OnConfirmNo()
    {
        confirmDialog.SetActive(false);
        pendingConfirmAction = null;
    }
    
    private void OnSaveComplete(string shipName)
    {
        saveStatusText.text = $"Saved '{shipName}' successfully!";
        saveStatusText.color = Color.green;
    }
    
    private void OnLoadComplete(string shipName)
    {
        loadStatusText.text = $"Loaded '{shipName}' successfully!";
        loadStatusText.color = Color.green;
    }
    
    private void OnError(string error)
    {
        if (savePanel.activeSelf)
        {
            saveStatusText.text = $"Error: {error}";
            saveStatusText.color = Color.red;
        }
        else if (loadPanel.activeSelf)
        {
            loadStatusText.text = $"Error: {error}";
            loadStatusText.color = Color.red;
        }
    }
    
    void OnDestroy()
    {
        if (saveLoadManager != null)
        {
            saveLoadManager.OnSaveComplete -= OnSaveComplete;
            saveLoadManager.OnLoadComplete -= OnLoadComplete;
            saveLoadManager.OnError -= OnError;
        }
    }
}