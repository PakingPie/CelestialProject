using UnityEngine;
using UnityEngine.UI;

public class CameraControlUI : MonoBehaviour
{
    [Header("References")]
    public ShipBuilderCamera cameraController;
    public ShipAssemblyManager assemblyManager;
    
    [Header("View Buttons")]
    public Button resetViewButton;
    public Button frameShipButton;
    public Button topViewButton;
    public Button frontViewButton;
    public Button sideViewButton;
    
    [Header("Settings")]
    public Slider zoomSlider;
    public Toggle autoRotateToggle;
    
    void Start()
    {
        SetupButtons();
        SetupSettings();
    }
    
    private void SetupButtons()
    {
        if (resetViewButton != null)
            resetViewButton.onClick.AddListener(() => cameraController.ResetView());
        
        if (frameShipButton != null)
            frameShipButton.onClick.AddListener(() => cameraController.FrameShip(assemblyManager));
        
        if (topViewButton != null)
            topViewButton.onClick.AddListener(() => SetPresetView(0f, 89f));
        
        if (frontViewButton != null)
            frontViewButton.onClick.AddListener(() => SetPresetView(0f, 0f));
        
        if (sideViewButton != null)
            sideViewButton.onClick.AddListener(() => SetPresetView(90f, 0f));
    }
    
    private void SetupSettings()
    {
        if (zoomSlider != null)
        {
            zoomSlider.minValue = cameraController.minDistance;
            zoomSlider.maxValue = cameraController.maxDistance;
            zoomSlider.value = cameraController.defaultDistance;
            zoomSlider.onValueChanged.AddListener(OnZoomSliderChanged);
        }
        
        if (autoRotateToggle != null)
        {
            autoRotateToggle.isOn = cameraController.autoRotateWhenIdle;
            autoRotateToggle.onValueChanged.AddListener(OnAutoRotateChanged);
        }
    }
    
    private void SetPresetView(float horizontal, float vertical)
    {
        // Use reflection or add public setters to camera controller
        // For now, we'll add a method to the camera controller
        cameraController.SetViewAngles(horizontal, vertical);
    }
    
    private void OnZoomSliderChanged(float value)
    {
        cameraController.SetZoom(value);
    }
    
    private void OnAutoRotateChanged(bool enabled)
    {
        cameraController.autoRotateWhenIdle = enabled;
    }
    
    void Update()
    {
        // Update zoom slider to match current zoom (for scroll wheel sync)
        if (zoomSlider != null && !cameraController.IsUserControlling)
        {
            zoomSlider.SetValueWithoutNotify(cameraController.Distance);
        }
    }
}