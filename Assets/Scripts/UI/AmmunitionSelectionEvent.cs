using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using UnityEditor.EditorTools;

public class AmmunitionSelectionEvent : MonoBehaviour
{
    [Header("Main Weapon Ammo")]
    public GameObject PlayerMainWeapons;
    public BulletPhysics MainGunHEAmmoPrefab;
    public BulletPhysics MainGunAPAmmoPrefab;
    public Button MainGunHEAmmoButton;
    public Button MainGunAPAmmoButton;

    [Header("Secondary Weapon Ammo")]
    public GameObject PlayerSecondaryWeapons;
    public BulletPhysics SecondaryGunHEAmmoPrefab;
    public BulletPhysics SecondaryGunAPAmmoPrefab;
    public BulletPhysics SecondaryGunPlasmaAmmoPrefab;
    public Button SecondaryGunHEAmmoButton;
    public Button SecondaryGunAPAmmoButton;
    public Button SecondaryGunPlasmaAmmoButton;

    [Header("Button Image Shader")]
    [Tooltip("Shader used for the button images to allow for dynamic styling.")]
    public Shader ButtonImageShader;

    private Color _mainGunAPColor = new Color(0.1f, 0.95f, 0.15f);
    private Color _mainGunHEColor = new Color(0.75f, 0.3f, 0.15f);
    private Color _secondaryGunAPColor = new Color(0.1f, 0.45f, 0.85f);
    private Color _secondaryGunHEColor = new Color(0.75f, 0.45f, 0.2f);
    private Color _secondaryGunPlasmaColor = new Color(0.85f, 0.1f, 0.9f);

    private void Awake()
    {
        // Main Gun Ammo Button Listeners
        MainGunHEAmmoButton.onClick.AddListener(OnMainGunHEAmmoButtonClicked);
        MainGunAPAmmoButton.onClick.AddListener(OnMainGunAPAmmoButtonClicked);

        MainGunAPAmmoButton.GetComponent<Image>().material = new Material(ButtonImageShader);
        SetButtonAttributes(MainGunAPAmmoButton, _mainGunAPColor, _mainGunAPColor, 0.4f, 0.2f, 1.0f, 0.15f);    // Use green color for main gun AP ammo
        MainGunAPAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.green;
        MainGunAPAmmoButton.transform.localScale = Vector3.one * 1.1f;

        MainGunHEAmmoButton.GetComponent<Image>().material = new Material(ButtonImageShader);
        SetButtonAttributes(MainGunHEAmmoButton, _mainGunHEColor, _mainGunHEColor, 0.2f, 0.2f, 1.0f, 0f);   // Use orange color for main gunHE ammo
        MainGunHEAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.orange;

        // Secondary Gun Ammo Button Listeners
        SecondaryGunHEAmmoButton.onClick.AddListener(OnSecondaryGunHEAmmoButtonClicked);
        SecondaryGunAPAmmoButton.onClick.AddListener(OnSecondaryGunAPAmmoButtonClicked);
        SecondaryGunPlasmaAmmoButton.onClick.AddListener(OnSecondaryGunPlasmaAmmoButtonClicked);

        SecondaryGunAPAmmoButton.GetComponent<Image>().material = new Material(ButtonImageShader);
        SetButtonAttributes(SecondaryGunAPAmmoButton, _secondaryGunAPColor, _secondaryGunAPColor, 0.4f, 0.2f, 1.0f, 0.15f); // Use blue color for secondary gun AP ammo
        SecondaryGunAPAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.cyan;
        SecondaryGunAPAmmoButton.transform.localScale = Vector3.one * 1.1f;
        
        SecondaryGunHEAmmoButton.GetComponent<Image>().material = new Material(ButtonImageShader);
        SetButtonAttributes(SecondaryGunHEAmmoButton, _secondaryGunHEColor, _secondaryGunHEColor, 0.2f, 0.2f, 1.0f, 0f); // Use light orange color for secondary gun HE ammo
        SecondaryGunHEAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.softYellow;
        SecondaryGunPlasmaAmmoButton.GetComponent<Image>().material = new Material(ButtonImageShader);
        SetButtonAttributes(SecondaryGunPlasmaAmmoButton, _secondaryGunPlasmaColor, _secondaryGunPlasmaColor, 0.2f, 0.2f, 1.0f, 0f); // Use purple color for secondary gun Plasma ammo
        SecondaryGunPlasmaAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.magenta;
    }

    public void SetButtonAttributes(Button button, Color strokeColor, Color contentColor, float strokeThickness, float fullScale, float strokeAlpha, float contentAlpha)
    {
        button.GetComponent<Image>().material.SetColor("_StrokeColor", strokeColor);
        button.GetComponent<Image>().material.SetColor("_ContentColor", contentColor);
        button.GetComponent<Image>().material.SetFloat("_StrokeThickness", strokeThickness);
        button.GetComponent<Image>().material.SetFloat("_FullSizeScale", fullScale);
        button.GetComponent<Image>().material.SetFloat("_StrokeAlpha", strokeAlpha);
        button.GetComponent<Image>().material.SetFloat("_ContentAlpha", contentAlpha);
    }

    private void OnMainGunAPAmmoButtonClicked()
    {
        SetAmmoToPlayerWeapons(MainGunAPAmmoPrefab.gameObject, PlayerMainWeapons);
        SetButtonAttributes(MainGunAPAmmoButton, _mainGunAPColor, _mainGunAPColor, 0.4f, 0.2f, 1.0f, 0.1f);
        MainGunAPAmmoButton.transform.localScale = Vector3.one * 1.1f;

        SetButtonAttributes(MainGunHEAmmoButton, _mainGunHEColor, _mainGunHEColor, 0.2f, 0.2f, 1.0f, 0f);
        MainGunHEAmmoButton.transform.localScale = Vector3.one;
    }

    private void OnMainGunHEAmmoButtonClicked()
    {
        SetAmmoToPlayerWeapons(MainGunHEAmmoPrefab.gameObject, PlayerMainWeapons);
        SetButtonAttributes(MainGunHEAmmoButton, _mainGunHEColor, _mainGunHEColor, 0.4f, 0.2f, 1.0f, 0.1f);
        MainGunHEAmmoButton.transform.localScale = Vector3.one * 1.1f;

        SetButtonAttributes(MainGunAPAmmoButton, _mainGunAPColor, _mainGunAPColor, 0.2f, 0.2f, 1.0f, 0f);
        MainGunAPAmmoButton.transform.localScale = Vector3.one;
    }

    private void OnSecondaryGunHEAmmoButtonClicked()
    {
        SetAmmoToPlayerWeapons(SecondaryGunHEAmmoPrefab.gameObject, PlayerSecondaryWeapons);
        SetButtonAttributes(SecondaryGunHEAmmoButton, _secondaryGunHEColor, _secondaryGunHEColor, 0.4f, 0.2f, 1.0f, 0.1f);
        SecondaryGunHEAmmoButton.transform.localScale = Vector3.one * 1.1f;

        SetButtonAttributes(SecondaryGunAPAmmoButton, _secondaryGunAPColor, _secondaryGunAPColor, 0.2f, 0.2f, 1.0f, 0f);
        SecondaryGunAPAmmoButton.transform.localScale = Vector3.one;

        SetButtonAttributes(SecondaryGunPlasmaAmmoButton, _secondaryGunPlasmaColor, _secondaryGunPlasmaColor, 0.2f, 0.2f, 1.0f, 0f);
        SecondaryGunPlasmaAmmoButton.transform.localScale = Vector3.one;
    }

    private void OnSecondaryGunAPAmmoButtonClicked()
    {
        SetAmmoToPlayerWeapons(SecondaryGunAPAmmoPrefab.gameObject, PlayerSecondaryWeapons);
        SetButtonAttributes(SecondaryGunAPAmmoButton, _secondaryGunAPColor, _secondaryGunAPColor, 0.4f, 0.2f, 1.0f, 0.1f);
        SecondaryGunAPAmmoButton.transform.localScale = Vector3.one * 1.1f;

        SetButtonAttributes(SecondaryGunHEAmmoButton, _secondaryGunHEColor, _secondaryGunHEColor, 0.2f, 0.2f, 1.0f, 0f);
        SecondaryGunHEAmmoButton.transform.localScale = Vector3.one;

        SetButtonAttributes(SecondaryGunPlasmaAmmoButton, _secondaryGunPlasmaColor, _secondaryGunPlasmaColor, 0.2f, 0.2f, 1.0f, 0f);
        SecondaryGunPlasmaAmmoButton.transform.localScale = Vector3.one;
    }

    private void OnSecondaryGunPlasmaAmmoButtonClicked()
    {
        SetAmmoToPlayerWeapons(SecondaryGunPlasmaAmmoPrefab.gameObject, PlayerSecondaryWeapons);
        SetButtonAttributes(SecondaryGunPlasmaAmmoButton, _secondaryGunPlasmaColor, _secondaryGunPlasmaColor, 0.4f, 0.2f, 1.0f, 0.1f);
        SecondaryGunPlasmaAmmoButton.transform.localScale = Vector3.one * 1.1f;

        SetButtonAttributes(SecondaryGunHEAmmoButton, _secondaryGunHEColor, _secondaryGunHEColor, 0.2f, 0.2f, 1.0f, 0f);
        SecondaryGunHEAmmoButton.transform.localScale = Vector3.one;

        SetButtonAttributes(SecondaryGunAPAmmoButton, _secondaryGunAPColor, _secondaryGunAPColor, 0.2f, 0.2f, 1.0f, 0f);
        SecondaryGunAPAmmoButton.transform.localScale = Vector3.one;
    }

    public void SetAmmoToPlayerWeapons(GameObject ammoPrefab, GameObject weaponParent)
    {
        var guns = weaponParent.GetComponentsInChildren<Gun>();
        foreach (var gun in guns)
        {
            gun.SetAmmoType(ammoPrefab);
        }
    }
}