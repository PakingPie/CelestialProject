using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField]
    private Camera _camera;
    [SerializeField]
    private RectTransform _selectionBox;
    [SerializeField]
    private LayerMask _unitMask;
    [SerializeField]
    private LayerMask _floorMask;
    [SerializeField]
    private float DragDelay = 0.1f;

    private float _dragDelay = 0.1f;
    private float _mouseDownTime;

    private Vector2 _startMousePosition;

    private HashSet<SelectableUnit> newlySelectedUnits = new HashSet<SelectableUnit>();
    private HashSet<SelectableUnit> deselectedUnits = new HashSet<SelectableUnit>();
    void Update()
    {
        HandleSelectionInputs();
    }

    private void HandleSelectionInputs()
    {
        if (Mouse.current.leftButton.isPressed && _mouseDownTime == 0f) 
        {
            _selectionBox.sizeDelta = Vector2.zero;
            _selectionBox.gameObject.SetActive(true);
            _startMousePosition = Mouse.current.position.ReadValue();
            _mouseDownTime = Time.time;
        }
        else if (Mouse.current.leftButton.isPressed && _mouseDownTime + _dragDelay < Time.time)
        {
            ResizeSelectionBox();
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            _selectionBox.sizeDelta = Vector2.zero;
            _selectionBox.gameObject.SetActive(false);

            foreach (var newUnit in newlySelectedUnits)
            {
                SelectionManager.Instance.Select(newUnit);
            }
            foreach (var deselectedUnit in deselectedUnits)
            {
                SelectionManager.Instance.Deselect(deselectedUnit);
            }

            newlySelectedUnits.Clear();
            deselectedUnits.Clear();

            if (Physics.Raycast(_camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit, _unitMask)
                && hit.collider.TryGetComponent<SelectableUnit>(out SelectableUnit unit))
            {
                if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
                {
                    if (SelectionManager.Instance.IsSelected(unit))
                    {
                        SelectionManager.Instance.Deselect(unit);
                    }
                    else
                    {
                        SelectionManager.Instance.Select(unit);
                    }
                }
                else
                {
                    SelectionManager.Instance.DeselectAll();
                    SelectionManager.Instance.Select(unit);
                }
            }
            else if (_mouseDownTime + _dragDelay > Time.time)
            {
                SelectionManager.Instance.DeselectAll();
            }

            _mouseDownTime = 0f;
        }
    }

    private void ResizeSelectionBox()
    {
        float width = Mouse.current.position.ReadValue().x - _startMousePosition.x;
        float height = Mouse.current.position.ReadValue().y - _startMousePosition.y;
        _selectionBox.anchoredPosition = _startMousePosition + new Vector2(width / 2, height / 2);
        _selectionBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));

        Bounds bounds = new Bounds(_selectionBox.anchoredPosition, _selectionBox.sizeDelta);
        for (int i = 0; i < SelectionManager.Instance.AvailableUnits.Count; i++)
        {
            SelectableUnit unit = SelectionManager.Instance.AvailableUnits[i];
            Vector2 screenPosition = _camera.WorldToScreenPoint(unit.transform.position);
            if (UnitIsInSelectionBox(screenPosition, bounds))
            {
                if (!SelectionManager.Instance.IsSelected(unit))
                {
                    newlySelectedUnits.Add(unit);
                }
                deselectedUnits.Remove(unit);
            }
            else
            {
                deselectedUnits.Add(unit);
                newlySelectedUnits.Remove(unit);
            }
        }
    }

    private bool UnitIsInSelectionBox(Vector2 Position, Bounds Bounds)
    {
        return Position.x > Bounds.min.x && Position.x < Bounds.max.x
            && Position.y > Bounds.min.y && Position.y < Bounds.max.y;
    }

}