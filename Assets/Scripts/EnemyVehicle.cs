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
    public Shader HealthBarShader;
    void Start()
    {
        HealthBar.GetComponent<Image>().material = new Material(HealthBarShader);
        HealthBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxHitPoints);
        HealthBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", HitPoints);
    }

    public override void Move()
    {
        // Implement movement logic for enemy vehicles
    }

    public override void Attack()
    {
        // Implement attack logic for enemy vehicles
    }

    public override void TakeDamage(int damage, AmmoType ammoType)
    {
        // Simple damage calculation; can be expanded based on ammoType and armor/shield
        switch (ammoType)
        {
            case AmmoType.Kinetic:
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
            case AmmoType.Energy:
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
            case AmmoType.Explosive:
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
        }

        HitPoints -= damage;
        // HealthBar.gameObject.GetComponent<Image
        HealthBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxHitPoints);
        HealthBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", HitPoints);

        if (HitPoints <= 0)
        {
            DestroyVehicle();
        }
    }

    public override void DestroyVehicle()
    {
        var boid = GetComponent<Boid>();
        var boidManager = FindAnyObjectByType<BoidsManager>();
        if (boid != null)
        {
            boidManager.RemoveBoid(boid);
        }

        Destroy(gameObject);
    }
}