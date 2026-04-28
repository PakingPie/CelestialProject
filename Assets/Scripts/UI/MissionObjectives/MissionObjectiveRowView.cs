using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionObjectiveRowView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _statusIconImage;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _contentText;

    [Header("Icons")]
    [SerializeField] private Sprite _activeIconSprite;
    [SerializeField] private Sprite _completedIconSprite;
    [SerializeField] private Sprite _failedIconSprite;
    [SerializeField] private string _activeIcon = "□";
    [SerializeField] private string _completedIcon = "▣";
    [SerializeField] private string _failedIcon = "☒";

    [Header("Style")]
    [SerializeField] private Color _textTint = new Color(0.65f, 1f, 1f, 1f);
    [Min(1f)]
    [SerializeField] private float _hdrIntensity = 2f;

    public void Apply(MissionObjectiveViewData data)
    {
        if (_contentText == null)
        {
            Debug.LogWarning($"{name}: Mission objective row is missing its content text reference.");
            return;
        }

        Color hdrColor = _textTint * _hdrIntensity;
        hdrColor.a = 1f;

        _contentText.color = hdrColor;
        ApplyStatusVisual(data.Status, hdrColor);

        _contentText.text = FormatContent(data);
    }

    private void ApplyStatusVisual(MissionObjectiveStatus status, Color color)
    {
        Sprite statusSprite = GetStatusSprite(status);
        bool useSpriteIcon = _statusIconImage != null && statusSprite != null;

        if (_statusIconImage != null)
        {
            _statusIconImage.gameObject.SetActive(useSpriteIcon);
            if (useSpriteIcon)
            {
                _statusIconImage.sprite = statusSprite;
                _statusIconImage.color = color;
            }
        }

        if (_statusText != null)
        {
            bool useTextFallback = !useSpriteIcon;
            _statusText.gameObject.SetActive(useTextFallback);
            if (useTextFallback)
            {
                _statusText.color = color;
                _statusText.text = GetStatusIcon(status);
            }
        }
    }

    private Sprite GetStatusSprite(MissionObjectiveStatus status)
    {
        switch (status)
        {
            case MissionObjectiveStatus.Completed:
                return _completedIconSprite;
            case MissionObjectiveStatus.Failed:
                return _failedIconSprite;
            default:
                return _activeIconSprite;
        }
    }

    private string GetStatusIcon(MissionObjectiveStatus status)
    {
        switch (status)
        {
            case MissionObjectiveStatus.Completed:
                return _completedIcon;
            case MissionObjectiveStatus.Failed:
                return _failedIcon;
            default:
                return _activeIcon;
        }
    }

    private static string FormatContent(MissionObjectiveViewData data)
    {
        if (string.IsNullOrWhiteSpace(data.Detail))
            return data.Title;

        return $"{data.Title} ({data.Detail})";
    }
}