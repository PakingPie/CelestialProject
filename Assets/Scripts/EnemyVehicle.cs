using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GlobalHelper;
using UnityEngine.UI;

[ExecuteAlways]
public class EnemyVehicle : VehicleBase
{
    public Faction FactionType = Faction.Foe;
    public VehicleType Type = VehicleType.Frigate;
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
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
        ArmorBar.GetComponent<Image>().material.SetVector("_Color1", Color.brown);
        ArmorBar.GetComponent<Image>().material.SetVector("_Color2", Color.brown);
        ArmorBar.GetComponent<Image>().material.SetVector("_Color3", Color.brown);

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
                    ArmorPoints -= damage / 2;
                    int armorFloatDamage = 0;
                    if (ArmorPoints < 0)
                    {
                        armorFloatDamage += -ArmorPoints / 2;
                        ArmorPoints = 0;
                    }

                    ShieldPoints -= damage * 2;
                    int shieldFloatDamage = 0;
                    if (ShieldPoints < 0)
                    {
                        shieldFloatDamage += -ShieldPoints / 2;
                        ShieldPoints = 0;
                    }

                    damage = armorFloatDamage + shieldFloatDamage;
                    break;
                }
            case AmmoType.Energy:   // Strong against armor, weak against shields
                {
                    ArmorPoints -= damage * 2;
                    int armorFloatDamage = 0;
                    if (ArmorPoints < 0)
                    {
                        armorFloatDamage += -ArmorPoints / 2;
                        ArmorPoints = 0;
                    }

                    ShieldPoints -= damage / 2;
                    int shieldFloatDamage = 0;
                    if (ShieldPoints < 0)
                    {
                        shieldFloatDamage += -ShieldPoints / 2;
                        ShieldPoints = 0;
                    }

                    damage = armorFloatDamage + shieldFloatDamage;
                    break;
                }
            case AmmoType.Explosive:    // Balanced damage to both armor and shields
                {
                    ArmorPoints -= damage;
                    int armorFloatDamage = 0;
                    if (ArmorPoints < 0)
                    {
                        armorFloatDamage += -ArmorPoints;
                        ArmorPoints = 0;
                    }

                    ShieldPoints -= damage;
                    int shieldFloatDamage = 0;
                    if (ShieldPoints < 0)
                    {
                        shieldFloatDamage += -ShieldPoints;
                        ShieldPoints = 0;
                    }

                    damage = armorFloatDamage + shieldFloatDamage;
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
                    // Plasma deals heavy damage to shields but less to armor
                    ShieldPoints -= damage * 3;
                    int shieldFloatDamage = 0;
                    if (ShieldPoints < 0)
                    {
                        shieldFloatDamage += -ShieldPoints / 2;
                        ShieldPoints = 0;
                    }

                    ArmorPoints -= damage / 2;
                    int armorFloatDamage = 0;
                    if (ArmorPoints < 0)
                    {
                        armorFloatDamage += -ArmorPoints / 2;
                        ArmorPoints = 0;
                    }

                    damage = armorFloatDamage + shieldFloatDamage;
                    break;
                }
            case AmmoType.Pierce:   // Ignores armor, shield, full damage to hit points
                {
                    break;
                }
        }

        HitPoints -= damage;
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