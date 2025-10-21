using UnityEngine;
using System.Collections.Generic;

public class SelectionManager
{
    private static SelectionManager _instance;
    public static SelectionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new SelectionManager();
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    public HashSet<SelectableUnit> SelectedUnits = new HashSet<SelectableUnit>();
    public List<SelectableUnit> AvailableUnits = new List<SelectableUnit>();

    private SelectionManager() { }

    public void Select(SelectableUnit unit)
    {
        SelectedUnits.Add(unit);
        unit.OnSelected();
    }

    public void Deselect(SelectableUnit unit)
    {
        unit.OnDeselected();
        SelectedUnits.Remove(unit);
    }

    public void DeselectAll()
    {
        foreach (var unit in SelectedUnits)
        {
            unit.OnDeselected();
        }
        SelectedUnits.Clear();
    }

    public bool IsSelected(SelectableUnit unit)
    {
        return SelectedUnits.Contains(unit);
    }
}