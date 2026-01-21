using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShipBuilderUI : MonoBehaviour
{
    [Header("References")]
    public ShipBuilderInputHandler inputHandler;
    public ShipAssemblyManager assemblyManager;
    public ShipComponentCatalog catalog;
    
    [Header("UI Panels")]
    public GameObject mainPanel;
    public Transform componentListContainer;
    public GameObject componentButtonPrefab;
    
    [Header("Category Tabs")]
    public Button bodyTabButton;
    public Button engineTabButton;
    public Button bridgeTabButton;
    public Button deckGunTabButton;
    
    [Header("Info Panel")]
    public GameObject infoPanel;
    public TextMeshProUGUI componentNameText;
    public TextMeshProUGUI componentStatsText;
    public Image componentIconImage;
    
    [Header("Ship Stats Panel")]
    public GameObject statsPanel;
    public TextMeshProUGUI totalHullText;
    public TextMeshProUGUI totalWeightText;
    public TextMeshProUGUI bodyCountText;
    
    [Header("Action Buttons")]
    public Button clearShipButton;
    public Button finishButton;
    public Button cancelPlacementButton;
    
    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipText;
    
    private ShipComponentType currentCategory = ShipComponentType.Body;
    private List<GameObject> spawnedButtons = new List<GameObject>();
    
    void Start()
    {
        SetupTabButtons();
        SetupActionButtons();
        SetupEventListeners();
        
        // Show body components by default
        ShowCategory(ShipComponentType.Body);
        UpdateShipStats();
        
        cancelPlacementButton.gameObject.SetActive(false);
    }
    
    private void SetupTabButtons()
    {
        bodyTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.Body));
        engineTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.Engine));
        bridgeTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.Bridge));
        deckGunTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.DeckGun));
    }
    
    private void SetupActionButtons()
    {
        clearShipButton.onClick.AddListener(OnClearShipClicked);
        finishButton.onClick.AddListener(OnFinishClicked);
        cancelPlacementButton.onClick.AddListener(OnCancelPlacementClicked);
    }
    
    private void SetupEventListeners()
    {
        inputHandler.OnComponentSelected += OnComponentSelected;
        inputHandler.OnComponentDeselected += OnComponentDeselected;
        inputHandler.OnPlacementComplete += OnPlacementComplete;
        inputHandler.OnAttachmentPointHovered += OnAttachmentPointHovered;
        
        assemblyManager.OnShipModified += UpdateShipStats;
    }
    
    /// <summary>
    /// Show components of a specific category
    /// </summary>
    public void ShowCategory(ShipComponentType category)
    {
        currentCategory = category;
        
        // Update tab visuals
        UpdateTabVisuals();
        
        // Clear existing buttons
        ClearComponentButtons();
        
        // Get components for this category
        List<ShipComponentData> components = catalog.GetComponentsByType(category);
        
        // Create buttons
        foreach (var component in components)
        {
            CreateComponentButton(component);
        }
    }
    
    private void UpdateTabVisuals()
    {
        // Highlight active tab
        SetTabActive(bodyTabButton, currentCategory == ShipComponentType.Body);
        SetTabActive(engineTabButton, currentCategory == ShipComponentType.Engine);
        SetTabActive(bridgeTabButton, currentCategory == ShipComponentType.Bridge);
        SetTabActive(deckGunTabButton, currentCategory == ShipComponentType.DeckGun);
    }
    
    private void SetTabActive(Button tab, bool active)
    {
        ColorBlock colors = tab.colors;
        colors.normalColor = active ? new Color(0.3f, 0.6f, 1f) : Color.white;
        tab.colors = colors;
    }
    
    private void ClearComponentButtons()
    {
        foreach (var button in spawnedButtons)
        {
            Destroy(button);
        }
        spawnedButtons.Clear();
    }
    
    private void CreateComponentButton(ShipComponentData component)
    {
        GameObject buttonObj = Instantiate(componentButtonPrefab, componentListContainer);
        spawnedButtons.Add(buttonObj);
        
        // Setup button
        Button button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => OnComponentButtonClicked(component));
        
        // Setup visuals
        ComponentButtonUI buttonUI = buttonObj.GetComponent<ComponentButtonUI>();
        if (buttonUI != null)
        {
            buttonUI.Setup(component);
        }
        else
        {
            // Fallback: try to find child elements
            var icon = buttonObj.GetComponentInChildren<Image>();
            if (icon != null && component.UiIcon != null)
                icon.sprite = component.UiIcon;
            
            var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = component.ComponentName;
        }
        
        // Add hover events
        var eventTrigger = buttonObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) => ShowComponentInfo(component));
        eventTrigger.triggers.Add(pointerEnter);
        
        var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((data) => HideComponentInfo());
        eventTrigger.triggers.Add(pointerExit);
    }
    
    private void OnComponentButtonClicked(ShipComponentData component)
    {
        inputHandler.SelectComponent(component);
    }
    
    private void ShowComponentInfo(ShipComponentData component)
    {
        if (infoPanel == null) return;
        
        infoPanel.SetActive(true);
        
        if (componentNameText != null)
            componentNameText.text = component.ComponentName;
        
        if (componentStatsText != null)
            componentStatsText.text = $"Hull: {component.HullPoints}\nWeight: {component.Weight}";
        
        if (componentIconImage != null && component.UiIcon != null)
            componentIconImage.sprite = component.UiIcon;
    }
    
    private void HideComponentInfo()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }
    
    private void OnComponentSelected(ShipComponentData component)
    {
        cancelPlacementButton.gameObject.SetActive(true);
        
        ShowTooltip($"Click to place {component.ComponentName}. Right-click to cancel.");
    }
    
    private void OnComponentDeselected()
    {
        cancelPlacementButton.gameObject.SetActive(false);
        HideTooltip();
    }
    
    private void OnPlacementComplete()
    {
        UpdateShipStats();
    }
    
    private void OnAttachmentPointHovered(AttachmentPoint point)
    {
        if (point == null) return;
        
        string typesText = string.Join(", ", point.acceptedTypes);
        ShowTooltip($"Attachment Point\nAccepts: {typesText}\nDirection: {point.direction}");
    }
    
    private void UpdateShipStats()
    {
        float totalHull = 0;
        float totalWeight = 0;
        
        foreach (var segment in assemblyManager.bodySegments)
        {
            if (segment.Data != null)
            {
                totalHull += segment.Data.HullPoints;
                totalWeight += segment.Data.Weight;
            }
        }
        
        if (assemblyManager.currentEngine?.Data != null)
        {
            totalHull += assemblyManager.currentEngine.Data.HullPoints;
            totalWeight += assemblyManager.currentEngine.Data.Weight;
        }
        
        if (assemblyManager.currentBridge?.Data != null)
        {
            totalHull += assemblyManager.currentBridge.Data.HullPoints;
            totalWeight += assemblyManager.currentBridge.Data.Weight;
        }
        
        foreach (var gun in assemblyManager.deckGuns)
        {
            if (gun.Data != null)
            {
                totalHull += gun.Data.HullPoints;
                totalWeight += gun.Data.Weight;
            }
        }
        
        if (totalHullText != null)
            totalHullText.text = $"Hull: {totalHull:F0}";
        
        if (totalWeightText != null)
            totalWeightText.text = $"Weight: {totalWeight:F1}";
        
        if (bodyCountText != null)
            bodyCountText.text = $"Bodies: {assemblyManager.bodySegments.Count}/{assemblyManager.maxBodySegments}";
    }
    
    private void OnClearShipClicked()
    {
        inputHandler.DeselectComponent();
        assemblyManager.ClearShip();
    }
    
    private void OnFinishClicked()
    {
        // Validate ship
        bool hasBody = assemblyManager.bodySegments.Count > 0;
        bool hasEngine = assemblyManager.currentEngine != null;
        bool hasBridge = assemblyManager.currentBridge != null;
        
        if (!hasBody || !hasEngine || !hasBridge)
        {
            ShowTooltip("Ship requires at least 1 body, 1 engine, and 1 bridge!");
            return;
        }
        
        // Ship is complete - trigger finish logic
        Debug.Log("Ship construction complete!");
        // You can raise an event or call a method here
    }
    
    private void OnCancelPlacementClicked()
    {
        inputHandler.DeselectComponent();
    }
    
    private void ShowTooltip(string message)
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(true);
            if (tooltipText != null)
                tooltipText.text = message;
        }
    }
    
    private void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
    
    void OnDestroy()
    {
        // Cleanup event listeners
        if (inputHandler != null)
        {
            inputHandler.OnComponentSelected -= OnComponentSelected;
            inputHandler.OnComponentDeselected -= OnComponentDeselected;
            inputHandler.OnPlacementComplete -= OnPlacementComplete;
            inputHandler.OnAttachmentPointHovered -= OnAttachmentPointHovered;
        }
        
        if (assemblyManager != null)
        {
            assemblyManager.OnShipModified -= UpdateShipStats;
        }
    }
}