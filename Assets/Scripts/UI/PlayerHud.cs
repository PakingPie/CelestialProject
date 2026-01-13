using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerHud : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovementController _cameraController = null;
    [SerializeField] private PlayerShipMovement _shipMovement = null;
    [SerializeField] private GunController _gunController = null;

    [Header("HUD Elements")]
    [SerializeField] private RectTransform _boresight = null;
    [SerializeField] private RectTransform _mousePos = null;
    [SerializeField] private RectTransform _velocityIndicator = null;

    [Header("Throttle Display")]
    [SerializeField] private UnityEngine.UI.Slider _throttleSlider = null;
    [SerializeField] private TMPro.TextMeshProUGUI _throttleText = null;

    [Header("Weapon Reload Display")]
    private List<Gun> _mainGuns;
    private List<Gun> _secondaryGuns;
    [SerializeField] private GameObject _reloadIndicatorPrefab;
    [SerializeField] private Transform _reloadIndicatorContainerForMainGuns;
    [SerializeField] private Transform _reloadIndicatorContainerForSecondaryGuns;
    
    private List<ReloadIndicator> _reloadIndicators = new List<ReloadIndicator>();
    private Camera _playerCam = null;

    private void Awake()
    {
        if (_cameraController == null)
        {
            _cameraController = FindAnyObjectByType<PlayerMovementController>();
            if (_cameraController == null)
                Debug.LogError(name + ": Hud - PlayerMovementController not found!");
        }

        if (_shipMovement == null)
        {
            _shipMovement = FindAnyObjectByType<PlayerShipMovement>();
        }

        if (_cameraController != null)
        {
            _playerCam = _cameraController.GetComponentInChildren<Camera>();
            if (_playerCam == null)
                Debug.LogError(name + ": Hud - No camera found on PlayerMovementController!");
        }
    }

    private void Start()
    {
        _mainGuns = _gunController.PrimaryGuns;
        _secondaryGuns = _gunController.SecondaryGuns;
        CreateReloadIndicators();
    }

    private void Update()
    {
        if (_cameraController == null || _playerCam == null)
            return;

        UpdateCrosshairs();
        UpdateThrottleDisplay();

        _reloadIndicatorContainerForMainGuns.gameObject.SetActive(_gunController.IsManualMode);
        _reloadIndicatorContainerForSecondaryGuns.gameObject.SetActive(_gunController.IsManualMode);
    }

    private void CreateReloadIndicators()
    {
        if (_reloadIndicatorPrefab == null || _reloadIndicatorContainerForMainGuns == null)
        {
            Debug.LogWarning("PlayerHud: Reload indicator prefab or container not assigned!");
            return;
        }

        // Clear existing indicators
        foreach (var indicator in _reloadIndicators)
        {
            if (indicator != null)
                Destroy(indicator.gameObject);
        }
        _reloadIndicators.Clear();

        // Create indicator for each gun
        for (int i = 0; i < _mainGuns.Count; i++)
        {
            if (_mainGuns[i] == null) continue;
            GameObject indicatorObj = Instantiate(_reloadIndicatorPrefab, _reloadIndicatorContainerForMainGuns);
            // Initialize with gun reference
            ReloadIndicator indicator = indicatorObj.GetComponent<ReloadIndicator>();
            indicator.Initialize(_mainGuns[i]);
            indicatorObj.GetComponent<Image>().material = indicator.ReloadCircleMaterial;
            _reloadIndicators.Add(indicator);
        }

        for (int i = 0; i < _secondaryGuns.Count; i++)
        {
            if (_secondaryGuns[i] == null) continue;
            GameObject indicatorObj = Instantiate(_reloadIndicatorPrefab, _reloadIndicatorContainerForSecondaryGuns);
            // Initialize with gun reference
            ReloadIndicator indicator = indicatorObj.GetComponent<ReloadIndicator>();
            indicator.Initialize(_secondaryGuns[i]);
            indicatorObj.GetComponent<Image>().material = indicator.ReloadCircleMaterial;
            _reloadIndicators.Add(indicator);
        }
    }

    private void UpdateCrosshairs()
    {
        if (_boresight != null)
        {
            Vector3 screenPos = _playerCam.WorldToScreenPoint(_cameraController.BoresightPos);
            _boresight.position = screenPos;
            _boresight.gameObject.SetActive(screenPos.z > 1f);
        }

        if (_mousePos != null)
        {
            Vector3 screenPos = _playerCam.WorldToScreenPoint(_cameraController.MouseAimPos);
            _mousePos.position = Mouse.current.position.ReadValue();
            _mousePos.gameObject.SetActive(screenPos.z > 1f);
        }

        if (_velocityIndicator != null && _shipMovement != null)
        {
            Vector3 velocityPoint = _shipMovement.transform.position + _shipMovement.Velocity.normalized * 500f;
            Vector3 screenPos = _playerCam.WorldToScreenPoint(velocityPoint);
            _velocityIndicator.position = screenPos;
            _velocityIndicator.gameObject.SetActive(screenPos.z > 1f && _shipMovement.Velocity.sqrMagnitude > 0.1f);
        }
    }

    private void UpdateThrottleDisplay()
    {
        if (_shipMovement == null) return;

        if (_throttleSlider != null)
        {
            _throttleSlider.value = _shipMovement.ThrottlePercent;
        }

        if (_throttleText != null)
        {
            _throttleText.text = $"{_shipMovement.Speed:F0}";
        }
    }

    public void SetReferenceMouseFlight(PlayerMovementController controller)
    {
        _cameraController = controller;
        if (_cameraController != null)
            _playerCam = _cameraController.GetComponentInChildren<Camera>();
    }

    /// <summary>
    /// Call this if guns change at runtime
    /// </summary>
    public void RefreshGuns(List<Gun> guns)
    {
        _mainGuns = guns;
        CreateReloadIndicators();
    }
}