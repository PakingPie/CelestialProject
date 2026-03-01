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
    public Button bowTabButton;
    public Button bodyTabButton;
    public Button sternTabButton;
    public Button engineTabButton;
    public Button bridgeTabButton;
    public Button weaponTabButton;
    
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
    public Button exportToPlayerButton;
    
    [Header("Export Settings")]
    [Tooltip("The player GameObject that will receive the exported ship as a child")]
    public GameObject targetPlayerObject;
    [Tooltip("The root GameObject containing all ship builder components (will be hidden on export)")]
    public GameObject shipBuilderRoot;
    
    [Header("Camera Settings")]
    [Tooltip("The camera used in ship builder mode (will be deactivated on export)")]
    public GameObject shipBuilderCamera;
    [Tooltip("The name of the camera rig child object under the player")]
    public GameObject PlayerCameraRig;
    
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
        
        inputHandler.DeselectComponent();
    }
    
    private void SetupTabButtons()
    {
        bowTabButton?.onClick.AddListener(() => ShowCategory(ShipComponentType.Bow));
        bodyTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.Body));
        sternTabButton?.onClick.AddListener(() => ShowCategory(ShipComponentType.Stern));
        engineTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.Engine));
        bridgeTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.Bridge));
        weaponTabButton.onClick.AddListener(() => ShowCategory(ShipComponentType.Weapon));
    }
    
    private void SetupActionButtons()
    {
        clearShipButton.onClick.AddListener(OnClearShipClicked);
        finishButton.onClick.AddListener(OnFinishClicked);
        cancelPlacementButton.onClick.AddListener(OnCancelPlacementClicked);
        
        if (exportToPlayerButton != null)
            exportToPlayerButton.onClick.AddListener(OnExportToPlayerClicked);
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
        if (bowTabButton != null) SetTabActive(bowTabButton, currentCategory == ShipComponentType.Bow);
        if (bodyTabButton != null) SetTabActive(bodyTabButton, currentCategory == ShipComponentType.Body);
        if (sternTabButton != null) SetTabActive(sternTabButton, currentCategory == ShipComponentType.Stern);
        if (engineTabButton != null) SetTabActive(engineTabButton, currentCategory == ShipComponentType.Engine);
        if (bridgeTabButton != null) SetTabActive(bridgeTabButton, currentCategory == ShipComponentType.Bridge);
        if (weaponTabButton != null) SetTabActive(weaponTabButton, currentCategory == ShipComponentType.Weapon);
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
    
    private void OnExportToPlayerClicked()
    {
        // Validate ship before export
        bool hasBody = assemblyManager.bodySegments.Count > 0;
        bool hasEngine = assemblyManager.currentEngine != null;
        bool hasBridge = assemblyManager.currentBridge != null;
        
        if (!hasBody || !hasEngine || !hasBridge)
        {
            ShowTooltip("Ship requires at least 1 body, 1 engine, and 1 bridge to export!");
            return;
        }
        
        if (targetPlayerObject == null)
        {
            ShowTooltip("No target player object assigned!");
            Debug.LogError("ShipBuilderUI: targetPlayerObject is not assigned!");
            return;
        }
        
        ExportShipToPlayer();
    }
    
    /// <summary>
    /// Export the built ship to the target player GameObject as a child
    /// </summary>
    private void ExportShipToPlayer()
    {
        if (assemblyManager.shipRoot == null)
        {
            Debug.LogError("ShipAssemblyManager shipRoot is null!");
            return;
        }
        
        // Create a container for the exported ship
        GameObject exportedShip = new GameObject("ExportedShip");
        exportedShip.transform.SetParent(targetPlayerObject.transform, false);
        exportedShip.transform.localPosition = Vector3.zero;
        exportedShip.transform.localRotation = Quaternion.identity;
        exportedShip.transform.localScale = Vector3.one;
        
        // Clone all ship components to the exported container
        foreach (Transform child in assemblyManager.shipRoot)
        {
            GameObject clonedComponent = Instantiate(child.gameObject, exportedShip.transform);
            clonedComponent.name = child.name; // Keep original names
            
            // Preserve local transform relative to ship root
            clonedComponent.transform.localPosition = child.localPosition;
            clonedComponent.transform.localRotation = child.localRotation;
            clonedComponent.transform.localScale = child.localScale;
        }
        
        // Set OwnerShip for all WeaponPlatforms and VehicleModules
        AssignOwnershipToComponents(exportedShip);
        
        // Assign stats to PlayerVehicle
        AssignStatsToPlayer();
        
        ShowTooltip("Ship exported to player successfully!");
        Debug.Log($"Ship exported to {targetPlayerObject.name} with {exportedShip.transform.childCount} components");
        
        // Hide the ship builder components
        HideShipBuilder();
    }
    
    /// <summary>
    /// Set OwnerShip reference on all WeaponPlatform and VehicleModule components
    /// </summary>
    private void AssignOwnershipToComponents(GameObject exportedShip)
    {
        // Find all WeaponPlatforms in the exported ship and set their OwnerShip
        WeaponPlatform[] weaponPlatforms = exportedShip.GetComponentsInChildren<WeaponPlatform>(true);
        foreach (var weapon in weaponPlatforms)
        {
            weapon.OwnerShip = targetPlayerObject;
        }
        Debug.Log($"Assigned OwnerShip to {weaponPlatforms.Length} WeaponPlatforms");
        
        // Find all VehicleModules in the exported ship and set their OwnerShip
        VehicleModule[] vehicleModules = exportedShip.GetComponentsInChildren<VehicleModule>(true);
        foreach (var module in vehicleModules)
        {
            module.OwnerShip = targetPlayerObject;
        }
        Debug.Log($"Assigned OwnerShip to {vehicleModules.Length} VehicleModules");
    }
    
    /// <summary>
    /// Calculate total stats from assembled ship and assign to PlayerVehicle
    /// </summary>
    private void AssignStatsToPlayer()
    {
        if (targetPlayerObject == null) return;
        
        PlayerVehicle playerVehicle = targetPlayerObject.GetComponent<PlayerVehicle>();
        if (playerVehicle == null)
        {
            Debug.LogWarning("No PlayerVehicle component found on target player object!");
            return;
        }
        
        // Calculate totals from all ship components
        float totalHull = 0;
        float totalArmor = 0;
        float totalShield = 0;
        float totalHullRegen = 0;
        float totalArmorRegen = 0;
        float totalShieldRegen = 0;
        
        // Body segments
        foreach (var segment in assemblyManager.bodySegments)
        {
            if (segment.Data != null)
            {
                totalHull += segment.Data.HullPoints;
                totalArmor += segment.Data.ArmorPoints;
                totalShield += segment.Data.ShieldPoints;
                totalHullRegen += segment.Data.HullRegenRate;
                totalArmorRegen += segment.Data.ArmorRegenRate;
                totalShieldRegen += segment.Data.ShieldRegenRate;
            }
        }
        
        // Engine
        if (assemblyManager.currentEngine?.Data != null)
        {
            totalHull += assemblyManager.currentEngine.Data.HullPoints;
            totalArmor += assemblyManager.currentEngine.Data.ArmorPoints;
            totalShield += assemblyManager.currentEngine.Data.ShieldPoints;
            totalHullRegen += assemblyManager.currentEngine.Data.HullRegenRate;
            totalArmorRegen += assemblyManager.currentEngine.Data.ArmorRegenRate;
            totalShieldRegen += assemblyManager.currentEngine.Data.ShieldRegenRate;
        }
        
        // Bridge
        if (assemblyManager.currentBridge?.Data != null)
        {
            totalHull += assemblyManager.currentBridge.Data.HullPoints;
            totalArmor += assemblyManager.currentBridge.Data.ArmorPoints;
            totalShield += assemblyManager.currentBridge.Data.ShieldPoints;
            totalHullRegen += assemblyManager.currentBridge.Data.HullRegenRate;
            totalArmorRegen += assemblyManager.currentBridge.Data.ArmorRegenRate;
            totalShieldRegen += assemblyManager.currentBridge.Data.ShieldRegenRate;
        }
        
        // Weapons
        foreach (var gun in assemblyManager.deckGuns)
        {
            if (gun.Data != null)
            {
                totalHull += gun.Data.HullPoints;
                totalArmor += gun.Data.ArmorPoints;
                totalShield += gun.Data.ShieldPoints;
                totalHullRegen += gun.Data.HullRegenRate;
                totalArmorRegen += gun.Data.ArmorRegenRate;
                totalShieldRegen += gun.Data.ShieldRegenRate;
            }
        }
        
        // Assign to PlayerVehicle
        playerVehicle.HitPoints = Mathf.RoundToInt(totalHull);
        playerVehicle.MaxHitPoints = Mathf.RoundToInt(totalHull);
        playerVehicle.ArmorPoints = Mathf.RoundToInt(totalArmor);
        playerVehicle.MaxArmorPoints = Mathf.RoundToInt(totalArmor);
        playerVehicle.ShieldPoints = Mathf.RoundToInt(totalShield);
        playerVehicle.MaxShieldPoints = Mathf.RoundToInt(totalShield);
        playerVehicle.HitPointsRegenerationRate = Mathf.RoundToInt(totalHullRegen);
        playerVehicle.ArmorRegenerationRate = Mathf.RoundToInt(totalArmorRegen);
        playerVehicle.ShieldRegenerationRate = Mathf.RoundToInt(totalShieldRegen);
        
        Debug.Log($"Assigned stats to player - Hull: {playerVehicle.MaxHitPoints}, Armor: {playerVehicle.MaxArmorPoints}, Shield: {playerVehicle.MaxShieldPoints}");
    }
    
    /// <summary>
    /// Hide all ship builder related GameObjects in the scene
    /// </summary>
    private void HideShipBuilder()
    {
        // Disable input handler first to stop camera rotation
        if (inputHandler != null)
        {
            inputHandler.enabled = false;
            Debug.Log("Ship builder input handler disabled");
        }
        
        if (shipBuilderRoot != null)
        {
            shipBuilderRoot.SetActive(false);
            Debug.Log("Ship builder hidden");
        }
        else
        {
            // Fallback: try to find and hide individual components
            if (mainPanel != null)
                mainPanel.SetActive(false);
            
            if (assemblyManager != null && assemblyManager.shipRoot != null)
                assemblyManager.shipRoot.gameObject.SetActive(false);
            
            Debug.LogWarning("ShipBuilderRoot not assigned. Only hiding UI panel and ship root.");
        }
        
        // Deactivate ship builder camera
        if (shipBuilderCamera != null)
        {
            shipBuilderCamera.SetActive(false);
            Debug.Log("Ship builder camera deactivated");
        }
        
        // Activate player's camera rig
        if (targetPlayerObject != null)
        {
            if (PlayerCameraRig != null)
            {
                PlayerCameraRig.SetActive(true);
                Debug.Log("Player camera rig activated");
            }
        }
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