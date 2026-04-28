using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerVehicleStatusHud : MonoBehaviour
{
    private static Sprite _defaultBarSprite;

    [Header("Source")]
    [SerializeField] private PlayerVehicle _playerVehicle;

    [Header("Bars")]
    [SerializeField] private Image _hullBar;
    [SerializeField] private Image _armorBar;
    [SerializeField] private Image _shieldBar;
    [SerializeField] private Color _hullBarColor = new Color(0.18f, 0.93f, 0.88f, 1f);
    [SerializeField] private Color _armorBarColor = new Color(1f, 0.92f, 0.18f, 1f);
    [SerializeField] private Color _shieldBarColor = new Color(0.92f, 0.96f, 1f, 1f);

    [Header("Values")]
    [SerializeField] private TMP_Text _hullValueText;
    [SerializeField] private TMP_Text _armorValueText;
    [SerializeField] private TMP_Text _shieldValueText;

    private RectTransform _hullBarRect;
    private RectTransform _armorBarRect;
    private RectTransform _shieldBarRect;
    private Vector2 _hullBarBaseSize;
    private Vector2 _armorBarBaseSize;
    private Vector2 _shieldBarBaseSize;

    private void OnEnable()
    {
        InitializeBars();

        if (Application.isPlaying)
            RefreshDisplay();
        else
            ApplyEditorPreview();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (!TryResolvePlayerVehicle())
            return;

        RefreshDisplay();
    }

    private void OnDestroy()
    {
    }

    public void Configure(
        PlayerVehicle playerVehicle,
        Image hullBar,
        Image armorBar,
        Image shieldBar,
        TMP_Text hullValueText,
        TMP_Text armorValueText,
        TMP_Text shieldValueText)
    {
        _playerVehicle = playerVehicle;
        _hullBar = hullBar;
        _armorBar = armorBar;
        _shieldBar = shieldBar;
        _hullValueText = hullValueText;
        _armorValueText = armorValueText;
        _shieldValueText = shieldValueText;

        InitializeBars();

        if (Application.isPlaying)
            RefreshDisplay();
        else
            ApplyEditorPreview();
    }

    private void OnValidate()
    {
        InitializeBars();

        if (!Application.isPlaying)
            ApplyEditorPreview();
    }

    private bool TryResolvePlayerVehicle()
    {
        if (_playerVehicle != null)
            return true;

        if (GameManager.Instance != null && GameManager.Instance.PlayerShip != null)
            _playerVehicle = GameManager.Instance.PlayerShip.GetComponent<PlayerVehicle>();

        if (_playerVehicle == null)
            _playerVehicle = FindAnyObjectByType<PlayerVehicle>();

        return _playerVehicle != null;
    }

    private void InitializeBars()
    {
        InitializeBar(_hullBar, _hullBarColor, ref _hullBarRect, ref _hullBarBaseSize);
        InitializeBar(_armorBar, _armorBarColor, ref _armorBarRect, ref _armorBarBaseSize);
        InitializeBar(_shieldBar, _shieldBarColor, ref _shieldBarRect, ref _shieldBarBaseSize);
    }

    private void RefreshDisplay()
    {
        if (!TryResolvePlayerVehicle())
        {
            SetValueText(_hullValueText, 0, 0);
            SetValueText(_armorValueText, 0, 0);
            SetValueText(_shieldValueText, 0, 0);
            UpdateBar(_hullBar, _hullBarRect, _hullBarBaseSize, _hullBarColor, 0, 0);
            UpdateBar(_armorBar, _armorBarRect, _armorBarBaseSize, _armorBarColor, 0, 0);
            UpdateBar(_shieldBar, _shieldBarRect, _shieldBarBaseSize, _shieldBarColor, 0, 0);
            return;
        }

        UpdateBar(_hullBar, _hullBarRect, _hullBarBaseSize, _hullBarColor, _playerVehicle.MaxHitPoints, _playerVehicle.HitPoints);
        UpdateBar(_armorBar, _armorBarRect, _armorBarBaseSize, _armorBarColor, _playerVehicle.MaxArmorPoints, _playerVehicle.ArmorPoints);
        UpdateBar(_shieldBar, _shieldBarRect, _shieldBarBaseSize, _shieldBarColor, _playerVehicle.MaxShieldPoints, _playerVehicle.ShieldPoints);

        SetValueText(_hullValueText, _playerVehicle.HitPoints, _playerVehicle.MaxHitPoints);
        SetValueText(_armorValueText, _playerVehicle.ArmorPoints, _playerVehicle.MaxArmorPoints);
        SetValueText(_shieldValueText, _playerVehicle.ShieldPoints, _playerVehicle.MaxShieldPoints);
    }

    private void ApplyEditorPreview()
    {
        if (TryResolvePlayerVehicle())
        {
            UpdateBar(_hullBar, _hullBarRect, _hullBarBaseSize, _hullBarColor, _playerVehicle.MaxHitPoints, _playerVehicle.HitPoints);
            UpdateBar(_armorBar, _armorBarRect, _armorBarBaseSize, _armorBarColor, _playerVehicle.MaxArmorPoints, _playerVehicle.ArmorPoints);
            UpdateBar(_shieldBar, _shieldBarRect, _shieldBarBaseSize, _shieldBarColor, _playerVehicle.MaxShieldPoints, _playerVehicle.ShieldPoints);

            SetValueText(_hullValueText, _playerVehicle.HitPoints, _playerVehicle.MaxHitPoints);
            SetValueText(_armorValueText, _playerVehicle.ArmorPoints, _playerVehicle.MaxArmorPoints);
            SetValueText(_shieldValueText, _playerVehicle.ShieldPoints, _playerVehicle.MaxShieldPoints);
            return;
        }

        UpdateBar(_hullBar, _hullBarRect, _hullBarBaseSize, _hullBarColor, 1, 1);
        UpdateBar(_armorBar, _armorBarRect, _armorBarBaseSize, _armorBarColor, 1, 1);
        UpdateBar(_shieldBar, _shieldBarRect, _shieldBarBaseSize, _shieldBarColor, 1, 1);
    }

    private static void InitializeBar(Image image, Color color, ref RectTransform rectTransform, ref Vector2 baseSize)
    {
        if (image == null)
            return;

        // if (_defaultBarSprite == null)
        //     _defaultBarSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");

        rectTransform = image.rectTransform;
        if (baseSize == Vector2.zero)
            baseSize = rectTransform.sizeDelta;

        image.material = null;
        image.sprite = _defaultBarSprite;
        image.type = Image.Type.Simple;
        image.fillAmount = 1f;
        image.color = color;
        image.raycastTarget = false;
    }

    private static void UpdateBar(Image image, RectTransform rectTransform, Vector2 baseSize, Color color, int maxAmount, int currentAmount)
    {
        if (image == null || rectTransform == null)
            return;

        int clampedMax = Mathf.Max(0, maxAmount);
        int clampedCurrent = Mathf.Clamp(currentAmount, 0, clampedMax);
        float normalized = clampedMax > 0 ? clampedCurrent / (float)clampedMax : 0f;

        image.material = null;
        image.sprite = _defaultBarSprite;
        image.type = Image.Type.Simple;
        image.color = color;

        rectTransform.sizeDelta = new Vector2(baseSize.x * normalized, baseSize.y);
    }

    private static void SetValueText(TMP_Text valueText, int currentAmount, int maxAmount)
    {
        if (valueText == null)
            return;

        valueText.text = $"{Mathf.Max(0, currentAmount)}/{Mathf.Max(0, maxAmount)}";
    }

}