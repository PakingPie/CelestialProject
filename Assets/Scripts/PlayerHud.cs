using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHud : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovementController _cameraController = null;
    [SerializeField] private PlayerShipMovement _shipMovement = null;

    [Header("HUD Elements")]
    [SerializeField] private RectTransform _boresight = null;
    [SerializeField] private RectTransform _mousePos = null;
    [SerializeField] private RectTransform _velocityIndicator = null;

    [Header("Throttle Display")]
    [SerializeField] private UnityEngine.UI.Slider _throttleSlider = null;
    [SerializeField] private TMPro.TextMeshProUGUI _throttleText = null;

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

    private void Update()
    {
        if (_cameraController == null || _playerCam == null)
            return;

        UpdateCrosshairs();
        UpdateThrottleDisplay();
    }

    private void UpdateCrosshairs()
    {
        // Boresight: where the ship is pointing
        if (_boresight != null)
        {
            Vector3 screenPos = _playerCam.WorldToScreenPoint(_cameraController.BoresightPos);
            _boresight.position = screenPos;
            _boresight.gameObject.SetActive(screenPos.z > 1f);
        }

        // Mouse position: where the camera is looking (for aiming)
        if (_mousePos != null)
        {
            Vector3 screenPos = _playerCam.WorldToScreenPoint(_cameraController.MouseAimPos);
            _mousePos.position = Mouse.current.position.ReadValue();
            _mousePos.gameObject.SetActive(screenPos.z > 1f);
        }

        // Velocity indicator: where the ship is actually moving
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
            _throttleSlider.value = _shipMovement.ThrustPercent;
        }

        if (_throttleText != null)
        {
            _throttleText.text = $"{_shipMovement.CurrentThrust:F0}";
        }
    }

    public void SetReferenceMouseFlight(PlayerMovementController controller)
    {
        _cameraController = controller;
        if (_cameraController != null)
            _playerCam = _cameraController.GetComponentInChildren<Camera>();
    }
}