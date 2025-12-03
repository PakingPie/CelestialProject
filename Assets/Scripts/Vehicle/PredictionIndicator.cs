using UnityEngine;
using UnityEngine.UI;

public class PredictionIndicator : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private RectTransform _rectTransform;
    
    private void Awake()
    {
        if (_image == null)
            _image = GetComponent<Image>();
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
    }
    
    public void Initialize(Color color)
    {
        if (_image != null)
            _image.color = color;
    }
    
    public void SetPosition(Vector3 screenPosition)
    {
        _rectTransform.position = screenPosition;
    }
    
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
    
    public void SetColor(Color color)
    {
        if (_image != null)
            _image.color = color;
    }
}