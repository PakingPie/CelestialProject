using UnityEngine;

public class SelectableUnit : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer _spriteRenderer;
    void Awake()
    {
        SelectionManager.Instance.AvailableUnits.Add(this);
    }

    public void OnSelected()
    {
        _spriteRenderer.gameObject.SetActive(true);
    }

    public void OnDeselected()
    {
        _spriteRenderer.gameObject.SetActive(false);
    }
}