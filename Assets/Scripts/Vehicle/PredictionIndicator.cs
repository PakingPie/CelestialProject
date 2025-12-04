using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PredictionIndicator : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private RectTransform _rectTransform;

    private Transform _playerTransform;
    private Transform _targetTransform; // Track the actual target

    private Vector3 _baseScale;
    private Camera _camera;

    private void Awake()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerTransform = player.transform;
            
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
    /// Set the target this indicator is tracking
    /// </summary>
    public void SetTarget(Transform target)
    {
        _targetTransform = target;
        UpdateDistanceText();
    }

    /// <summary>
    /// Set position in world space with billboard effect (always faces camera)
    /// </summary>
    public void SetWorldPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        FaceCamera();

        if(_text != null)
            UpdateDistanceText();
    }

    /// <summary>
    /// Update the distance text based on target position
    /// </summary>
    private void UpdateDistanceText()
    {
        if (_text == null || _playerTransform == null) return;

        if (_targetTransform != null)
        {
            float distance = Vector3.Distance(_playerTransform.position, _targetTransform.position);
            _text.text = FormatDistance(distance);
        }
    }

    private string FormatDistance(float distance)
    {
        if (distance >= 1000f)
        {
            return (distance / 1000f).ToString("F1") + "km";
        }
        return distance.ToString("F0") + "m";
    }

    /// <summary>
    /// Make the indicator face the camera (billboard)
    /// </summary>
    public void FaceCamera()
    {
        if (_camera != null)
        {
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

    public Transform GetTarget()
    {
        return _targetTransform;
    }
}