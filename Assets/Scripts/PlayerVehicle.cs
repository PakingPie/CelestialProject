using UnityEngine;
using UnityEngine.UI;


public class PlayerVehicle : VehicleBase
{
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;
    
    public int ShieldRegenerationRate = 1; // Points per second
    public float ShieldRegenerationDelay = 5f; // Seconds after taking damage before regeneration starts
    private float _shieldRegenTimer = 0f;

    public override void RestoreShield()
    {
        ShieldPoints += ShieldRegenerationRate;
        if (ShieldPoints > MaxShieldPoints)
        {
            ShieldPoints = MaxShieldPoints;
        }
        ShieldBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ShieldPoints);
        ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Strength", ShieldPoints / (float)MaxShieldPoints);
    }
}