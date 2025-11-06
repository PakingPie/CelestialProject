//
// Copyright (c) Brian Hernandez. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using UnityEngine;


public class PlayerHud : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovementController _mouseFlight = null;

    [Header("HUD Elements")]
    [SerializeField] private RectTransform _boresight = null;
    [SerializeField] private RectTransform _mousePos = null;

    private Camera _playerCam = null;

    private void Awake()
    {
        if (_mouseFlight == null)
            Debug.LogError(name + ": Hud - Mouse Flight Controller not assigned!");

        _playerCam = _mouseFlight.GetComponentInChildren<Camera>();

        if (_playerCam == null)
            Debug.LogError(name + ": Hud - No camera found on assigned Mouse Flight Controller!");
    }

    private void Update()
    {
        if (_mouseFlight == null || _playerCam == null)
            return;

        UpdateGraphics(_mouseFlight);
    }

    private void UpdateGraphics(PlayerMovementController controller)
    {
        if (_boresight != null)
        {
            _boresight.position = _playerCam.WorldToScreenPoint(controller.BoresightPos);
            _boresight.gameObject.SetActive(_boresight.position.z > 1f);
        }

        if (_mousePos != null)
        {
            _mousePos.position = _playerCam.WorldToScreenPoint(controller.MouseAimPos);
            _mousePos.gameObject.SetActive(_mousePos.position.z > 1f);
        }
    }

    public void SetReferenceMouseFlight(PlayerMovementController controller)
    {
        _mouseFlight = controller;
    }
}
