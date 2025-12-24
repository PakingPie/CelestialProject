using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class AmmunitionSelectionEvent : MonoBehaviour
{
    public GameObject PlayerMainWeapons;
    public BulletPhysics HEAmmoPrefab;
    public BulletPhysics APAmmoPrefab;
    public Button HEAmmoButton;
    public Button APAmmoButton;
    public Shader ButtonImageShader;

    private void Awake()
    {
        HEAmmoButton.onClick.AddListener(OnHEAmmoButtonClicked);
        APAmmoButton.onClick.AddListener(OnAPAmmoButtonClicked);
        APAmmoButton.GetComponent<Image>().material = new Material(ButtonImageShader);
        
        APAmmoButton.GetComponent<Image>().material.SetFloat("_StrokeThickness", 0.4f);
        APAmmoButton.GetComponent<Image>().material.SetFloat("_StrokeAlpha", 1.0f);
        APAmmoButton.GetComponent<Image>().material.SetVector("_EdgeMinMax", new Vector4(0.0f, 0.6f, 0.0f, 0.0f));
        APAmmoButton.GetComponent<Image>().material.SetFloat("_ContentAlpha", 0.2f);

        APAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.green;

        HEAmmoButton.GetComponent<Image>().material = new Material(ButtonImageShader);
        HEAmmoButton.GetComponent<Image>().material.SetFloat("_StrokeThickness", 0.4f);
        HEAmmoButton.GetComponent<Image>().material.SetFloat("_StrokeAlpha", 1.0f);
        HEAmmoButton.GetComponent<Image>().material.SetVector("_EdgeMinMax", new Vector4(0.0f, 0.6f, 0.0f, 0.0f));
        HEAmmoButton.GetComponent<Image>().material.SetFloat("_ContentAlpha", 0.0f);
        
    }

    private void OnAPAmmoButtonClicked()
    {
        SetAmmoToPlayerMainWeapons(APAmmoPrefab.gameObject);
        APAmmoButton.GetComponent<Image>().material.SetFloat("_ContentAlpha", 0.2f);
        APAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.green;
        APAmmoButton.transform.localScale = Vector3.one * 1.1f;
        HEAmmoButton.GetComponent<Image>().material.SetFloat("_ContentAlpha", 0f);
        HEAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
        HEAmmoButton.transform.localScale = Vector3.one;
    }

    private void OnHEAmmoButtonClicked()
    {
        SetAmmoToPlayerMainWeapons(HEAmmoPrefab.gameObject);
        HEAmmoButton.GetComponent<Image>().material.SetFloat("_ContentAlpha", 0.2f);
        HEAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.red;
        HEAmmoButton.transform.localScale = Vector3.one * 1.1f;
        APAmmoButton.GetComponent<Image>().material.SetFloat("_ContentAlpha", 0f);
        APAmmoButton.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
        APAmmoButton.transform.localScale = Vector3.one;
    }

    public void SetAmmoToPlayerMainWeapons(GameObject ammoPrefab)
    {
        var mainGuns = PlayerMainWeapons.GetComponentsInChildren<Gun>();
        foreach (var gun in mainGuns)
        {
            gun.SetAmmoType(ammoPrefab);
        }
    }
}