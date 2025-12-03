using UnityEngine;
using UnityEngine.UI;

public class PredictionIndicator : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private RectTransform _rectTransform;

    private Vector3 _baseScale;
    private Camera _camera;

    private void Awake()
    {
        if (_image == null)
            _image = GetComponent<Image>();
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        _baseScale = transform.localScale;
        _camera = Camera.main;
    }

    public void Initialize(Color color, Camera camera = null)
    {
        if (_image != null)
            _image.color = color;

        _baseScale = transform.localScale;
        _camera = camera != null ? camera : Camera.main;
    }

    /// <summary>
    /// Set position in world space with billboard effect (always faces camera)
    /// </summary>
    public void SetWorldPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        FaceCamera();
    }

    /// <summary>
    /// Make the indicator face the camera (billboard)
    /// </summary>
    public void FaceCamera()
    {
        if (_camera != null)
        {
            // Face the same direction as camera (not look at camera)
            transform.rotation = _camera.transform.rotation;
        }
    }

    /// <summary>
    /// Set position using screen coordinates (for Screen Space canvas)
    /// </summary>
    public void SetPosition(Vector3 screenPosition)
    {
        if (_rectTransform != null)
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

    public void SetScale(float scale)
    {
        transform.localScale = _baseScale * scale;
    }

    public void SetAlpha(float alpha)
    {
        if (_image != null)
        {
            Color c = _image.color;
            c.a = alpha;
            _image.color = c;
        }
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }
}