using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTargeting : MonoBehaviour
{
    [Header("UI")]
    public GameObject TargetingBoxPrefab;
    public Canvas TargetingCanvas;

    [Header("References")]
    public GameObject PlayerShip;
    public LayerMask TargetableLayers = -1;
    public float MaxTargetingRange = 8000f;

    [Header("Targeting Settings")]
    [SerializeField] private float screenTargetingRadius = 50f; // Pixels
    [SerializeField] private bool preferClosestToCenter = true;

    [Header("Debug")]
    public VehicleBase CurrentTarget;

    private List<WeaponBase> _playerWeapons = new List<WeaponBase>();
    private Camera _mainCamera;
    private GameObject _targetingBox;
    private RectTransform _targetingBoxRect;

    void Start()
    {
        _mainCamera = Camera.main;
        CreateTargetingBox();
        // Delay weapon list refresh to ensure all weapons are initialized
        Invoke(nameof(RefreshWeaponsList), 0.1f);
    }

    void CreateTargetingBox()
    {
        if (TargetingBoxPrefab == null || TargetingCanvas == null) return;

        _targetingBox = Instantiate(TargetingBoxPrefab, TargetingCanvas.transform);
        _targetingBoxRect = _targetingBox.GetComponent<RectTransform>();

        if (_targetingBoxRect == null)
        {
            Debug.LogError("TargetingBoxPrefab is missing RectTransform!");
        }

        _targetingBox.SetActive(false);
    }

    void Update()
    {
        HandleTargetingInput();
    }

    void LateUpdate()
    {
        UpdateTargetingBoxPosition();
    }

    void UpdateTargetingBoxPosition()
    {
        if (!CurrentTarget || _targetingBox == null || _mainCamera == null)
        {
            if (_targetingBox != null && _targetingBox.activeSelf)
                _targetingBox.SetActive(false);
            return;
        }

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(CurrentTarget.transform.position);

        if (screenPos.z < 0)
        {
            _targetingBox.SetActive(false);
            return;
        }

        _targetingBox.SetActive(true);

        // Convert screen position to canvas local position
        RectTransform canvasRect = TargetingCanvas.GetComponent<RectTransform>();
        Vector2 localPoint;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            TargetingCanvas.worldCamera,  // Use null for Overlay canvas
            out localPoint))
        {
            _targetingBoxRect.anchoredPosition = localPoint;
        }
    }

    void HandleTargetingInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectTarget();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            ClearTarget();
        }
    }

    void TrySelectTarget()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);

        // First try direct raycast
        if (Physics.Raycast(ray, out RaycastHit directHit, MaxTargetingRange, TargetableLayers))
        {
            VehicleBase directTarget = directHit.collider.GetComponentInParent<VehicleBase>();
            if (directTarget != null && IsValidTarget(directTarget))
            {
                SetTargetForAllWeapons(directTarget);
                return;
            }
        }

        // If no direct hit, find closest target within screen radius
        VehicleBase bestTarget = FindClosestTargetInScreenRadius(mousePos, screenTargetingRadius);

        if (bestTarget != null)
        {
            SetTargetForAllWeapons(bestTarget);
        }
    }

    VehicleBase FindClosestTargetInScreenRadius(Vector2 screenCenter, float radius)
    {
        VehicleBase bestTarget = null;
        float bestScore = float.MaxValue;

        // Find all potential targets in range
        Collider[] colliders = Physics.OverlapSphere(
            _mainCamera.transform.position,
            MaxTargetingRange,
            TargetableLayers);

        foreach (var col in colliders)
        {
            VehicleBase target = col.GetComponentInParent<VehicleBase>();

            if (target == null || !IsValidTarget(target))
                continue;

            // Get screen position
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(target.transform.position);

            // Skip if behind camera
            if (screenPos.z < 0)
                continue;

            // Check distance from cursor
            float screenDist = Vector2.Distance(screenCenter, new Vector2(screenPos.x, screenPos.y));

            if (screenDist > radius)
                continue;

            // Score: prefer closer to cursor center, and closer in world space
            float worldDist = Vector3.Distance(_mainCamera.transform.position, target.transform.position);
            float score = preferClosestToCenter
                ? screenDist + (worldDist * 0.01f)  // Prioritize screen proximity
                : worldDist;                         // Prioritize world proximity

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    bool IsValidTarget(VehicleBase target)
    {
        if (target.FactionType == GlobalHelper.Faction.Player ||
            target.FactionType == GlobalHelper.Faction.Ally)
        {
            return false;
        }

        if (PlayerShip != null && target.gameObject == PlayerShip)
        {
            return false;
        }

        return true;
    }

    void SetTargetForAllWeapons(VehicleBase target)
    {
        CurrentTarget = target;

        for (int i = 0; i < _playerWeapons.Count; i++)
        {
            if (_playerWeapons[i] != null)
            {
                _playerWeapons[i].SetTarget(target.transform, lockTarget: true);
            }
        }

        // Show targeting box
        if (_targetingBox != null)
        {
            _targetingBox.SetActive(true);
        }
    }

    void ClearTarget()
    {
        CurrentTarget = null;

        for (int i = 0; i < _playerWeapons.Count; i++)
        {
            if (_playerWeapons[i] != null)
            {
                _playerWeapons[i].ClearManualTarget();
            }
        }

        // Hide targeting box
        if (_targetingBox != null)
        {
            _targetingBox.SetActive(false);
        }
    }

    public void RefreshWeaponsList()
    {
        _playerWeapons.Clear();

        if (PlayerShip != null)
        {
            PlayerShip.GetComponentsInChildren(true, _playerWeapons);
        }
    }
}