using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GlobalHelper;
using UnityEngine.UI;
using Unity.Burst.Intrinsics;

[ExecuteAlways]
public class EnemyVehicle : VehicleBase
{
    public Faction FactionType = Faction.Foe;
    public VehicleType Type = VehicleType.Frigate;
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
    public GameObject Turret;
    public Transform FireSpawn;
    public int ShieldRegenerationRate = 1; // Points per second
    void Start()
    {
        HealthBar.GetComponent<Image>().material = new Material(HealthBarShader);
        HealthBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxHitPoints);
        HealthBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", HitPoints);
        HealthBar.GetComponent<Image>().material.SetVector("_Color1", Color.green);
        HealthBar.GetComponent<Image>().material.SetVector("_Color2", Color.yellow);
        HealthBar.GetComponent<Image>().material.SetVector("_Color3", Color.red);

        ArmorBar.GetComponent<Image>().material = new Material(HealthBarShader);
        ArmorBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxArmorPoints);
        ArmorBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ArmorPoints);
        ArmorBar.GetComponent<Image>().material.SetVector("_Color1", Color.yellow);
        ArmorBar.GetComponent<Image>().material.SetVector("_Color2", Color.yellow);
        ArmorBar.GetComponent<Image>().material.SetVector("_Color3", Color.yellow);

        ShieldBar.GetComponent<Image>().material = new Material(HealthBarShader);
        ShieldBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxShieldPoints);
        ShieldBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ShieldPoints);
        ShieldBar.GetComponent<Image>().material.SetVector("_Color1", Color.cyan);
        ShieldBar.GetComponent<Image>().material.SetVector("_Color2", Color.cyan);
        ShieldBar.GetComponent<Image>().material.SetVector("_Color3", Color.cyan);
    }

    public override void Move()
    {
        // Implement movement logic for enemy vehicles
    }

    public override void Attack()
    {
        // Implement attack logic for enemy vehicles

    }

    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
        // Simple damage calculation; can be expanded based on ammoType and armor/shield
        switch (ammoType)
        {
            case AmmoType.Kinetic:  // Strong against shields, weak against armor
                {
                    int armorFloatDamage = 0;
                    if (ArmorPoints > 0)
                    {
                        ArmorPoints -= damage / 2;
                        if (ArmorPoints <= 0)
                        {
                            armorFloatDamage += -ArmorPoints / 2;
                            ArmorPoints = 0;
                        }
                    }

                    int shieldFloatDamage = 0;
                    if (ShieldPoints > 0)
                    {
                        ShieldPoints -= damage * 2;
                        if (ShieldPoints <= 0)
                        {
                            shieldFloatDamage += -ShieldPoints / 2;
                            ShieldPoints = 0;
                        }
                    }


                    if (ArmorPoints <= 0 && ShieldPoints <= 0)  // Both armor and shield are down, take full damage plus bonus damage
                    {
                        damage = (int)(damage * 1.5f) + armorFloatDamage + shieldFloatDamage;
                    }
                    else if (ArmorPoints > 0 && ShieldPoints <= 0) // Only shield is down, take half damage
                    {
                        damage = (int)(damage * 0.5f) + shieldFloatDamage;
                    }
                    else if (ArmorPoints > 0 && ShieldPoints > 0)  // Both armor and shield are still up, take no damage
                    {
                        damage = 0;
                    }
                    break;
                }
            case AmmoType.Energy:   // Strong against armor, weak against shields; 50% damage bonus if both are down
                {
                    int armorFloatDamage = 0;
                    if (ArmorPoints > 0)
                    {
                        ArmorPoints -= damage * 2;
                        if (ArmorPoints <= 0)
                        {
                            armorFloatDamage += -ArmorPoints / 2;
                            ArmorPoints = 0;
                        }
                    }

                    int shieldFloatDamage = 0;
                    if (ShieldPoints > 0)
                    {
                        ShieldPoints -= damage / 2;
                        if (ShieldPoints <= 0)
                        {
                            shieldFloatDamage += -ShieldPoints / 2;
                            ShieldPoints = 0;
                        }
                    }

                    if (ArmorPoints <= 0 && ShieldPoints <= 0)  // Both armor and shield are down, take full damage plus bonus damage
                    {
                        damage = (int)(damage * 1.5f) + armorFloatDamage + shieldFloatDamage;
                    }
                    else if (ArmorPoints <= 0 && ShieldPoints > 0) // Only armor is down, take half damage
                    {
                        damage = (int)(damage * 0.5f) + armorFloatDamage;
                    }
                    else if (ArmorPoints > 0 && ShieldPoints > 0)  // Both armor and shield are still up, take no damage
                    {
                        damage = 0;
                    }
                    break;
                }
            case AmmoType.Explosive:    // Balanced damage to both armor and shields
                {
                    int armorFloatDamage = 0;
                    if (ArmorPoints > 0)
                    {
                        ArmorPoints -= damage;
                        if (ArmorPoints <= 0)
                        {
                            armorFloatDamage += -ArmorPoints;
                            ArmorPoints = 0;
                        }
                    }

                    int shieldFloatDamage = 0;
                    if (ShieldPoints > 0)
                    {
                        ShieldPoints -= damage;
                        if (ShieldPoints <= 0)
                        {
                            shieldFloatDamage += -ShieldPoints;
                            ShieldPoints = 0;
                        }
                    }

                    if (ArmorPoints <= 0 && ShieldPoints <= 0) // Both armor and shield are down, deal extra damage to hit points
                    {
                        damage *= 2;
                    }
                    else if (ArmorPoints <= 0 && ShieldPoints > 0) // Only armor is down, take half damage
                    {
                        damage = (int)(damage * 0.5f);
                    }
                    else if (ShieldPoints <= 0 && ArmorPoints > 0) // Only shield is down, take three-quarters damage
                    {
                        damage = (int)(damage * 0.75f);
                    }
                    else if (ArmorPoints > 0 && ShieldPoints > 0) // Both armor and shield are still up, take quarter damage
                    {
                        damage = (int)(damage * 0.25f);
                    }
                    break;
                }
            case AmmoType.EMP:  // Effective against shields, no direct damage
                {
                    // EMP does not deal direct damage but can disable shields or systems
                    ShieldPoints = 0;
                    break;
                }
            case AmmoType.Plasma:   // Heavy against shields, light against armor
                {
                    int armorFloatDamage = 0;
                    if (ArmorPoints > 0)
                    {
                        ArmorPoints -= damage / 2;
                        if (ArmorPoints <= 0)
                        {
                            armorFloatDamage += -ArmorPoints / 2;
                            ArmorPoints = 0;
                        }
                    }

                    int shieldFloatDamage = 0;
                    if (ShieldPoints > 0)
                    {
                        ShieldPoints -= damage * 3;
                        if (ShieldPoints <= 0)
                        {
                            shieldFloatDamage += -ShieldPoints / 2;
                            ShieldPoints = 0;
                        }
                    }


                    if (ArmorPoints <= 0 && ShieldPoints <= 0)  // Both armor and shield are down, take full damage plus bonus damage
                    {
                        damage = (int)(damage * 1.25f) + armorFloatDamage + shieldFloatDamage;
                    }
                    else if (ArmorPoints <= 0 && ShieldPoints > 0) // Only shield is down, take half damage
                    {
                        damage = (int)(damage * 0.5f) + shieldFloatDamage;
                    }
                    else if (ArmorPoints > 0 && ShieldPoints > 0)  // Both armor and shield are still up, take no damage
                    {
                        damage = 0;
                    }
                    break;
                }
            case AmmoType.Pierce:   // Ignores armor, shield, full damage to hit points
                {
                    break;
                }
        }

        HitPoints -= damage;
        // Debug.Log($"EnemyVehicle took {damage} damage, remaining HP: {HitPoints}");
        HealthBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", HitPoints);
        ArmorBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ArmorPoints);
        ShieldBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ShieldPoints);

        if (HitPoints <= 0)
        {
            DestroyVehicle();
        }

        return HitPoints > 0;
    }

    public override void DestroyVehicle()
    {
        var boid = GetComponent<Boid>();
        var boidManager = FindAnyObjectByType<BoidsManager>();
        if (boid != null)
        {
            boidManager.RemoveBoid(boid);
        }

        Destroy(gameObject, 0.5f);
    }
}