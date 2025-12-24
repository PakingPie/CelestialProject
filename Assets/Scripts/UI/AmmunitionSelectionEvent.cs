using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class AmmunitionSelectionEvent : MonoBehaviour
{
    public GameObject PlayerMainWeapons;
    public BulletPhysics HEAmmoPrefab;
    public BulletPhysics APAmmoPrefab;
    private UIDocument uIDocument;
    public Button HEAmmoButton;
    public Button APAmmoButton;

    private void Awake()
    {
        uIDocument = GetComponent<UIDocument>();
        var root = uIDocument.rootVisualElement;

        APAmmoButton = root.Q<Button>("APAmmoButton");
        APAmmoButton.RegisterCallback<ClickEvent>(OnAPAmmoButtonClicked);

        HEAmmoButton = root.Q<Button>("HEAmmoButton");
        HEAmmoButton.RegisterCallback<ClickEvent>(OnHEAmmoButtonClicked);
    }

    private void OnAPAmmoButtonClicked(ClickEvent evt)
    {
        // Implement logic for selecting AP ammunition
        Debug.Log("AP Ammo button clicked!");
        SetAmmoToPlayerMainWeapons(APAmmoPrefab.gameObject);
    }

    private void OnHEAmmoButtonClicked(ClickEvent evt)
    {
        Debug.Log("HE Ammo button clicked!");
        SetAmmoToPlayerMainWeapons(HEAmmoPrefab.gameObject);
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