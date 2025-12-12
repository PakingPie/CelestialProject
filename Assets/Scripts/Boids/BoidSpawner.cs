using System.Collections.Generic;
using UnityEngine;

public class BoidSpawner : MonoBehaviour
{
    public enum GizmoType
    {
        Never, SelectedOnly, Always
    }

    [Header("Spawn Settings")]
    public Boid[] prefabs;
    public float spawnRadius = 10.0f;
    public int spawnCount = 10;
    public Color color;
    public GizmoType showSpawnRegion;
    public Vector2 HeightRange = new Vector2(-1.0f, 1.0f);
    public List<GameObject> SpawnedObjects;
    [Header("Attack Behavior")]
    public BoidAttackProfile attackProfile;
    void Awake()
    {
        SpawnedObjects = new List<GameObject>();
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 randomSphere = Random.insideUnitSphere * spawnRadius;
            Vector3 pos = transform.position + new Vector3(randomSphere.x, Mathf.Clamp(randomSphere.y, HeightRange.x, HeightRange.y), randomSphere.z); // originly Random.insideUnitSphere
            Boid boid = Instantiate(prefabs[Random.Range(0, prefabs.Length)]);
            SpawnedObjects.Add(boid.gameObject);
            boid.transform.position = pos;
            randomSphere = Random.insideUnitSphere;
            boid.transform.forward = new Vector3(randomSphere.x, Mathf.Clamp(randomSphere.y, HeightRange.x, HeightRange.y), randomSphere.z); // originly Random.insideUnitSphere
            boid.SetColor(color);
            
            if (attackProfile != null)
            {
                var attackBehavior = boid.GetComponent<BoidAttackBehavior>();
                if (attackBehavior == null)
                    attackBehavior = boid.gameObject.AddComponent<BoidAttackBehavior>();
                attackBehavior.SetProfile(attackProfile);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (showSpawnRegion == GizmoType.Always)
        {
            DrawGizmos();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (showSpawnRegion == GizmoType.SelectedOnly)
        {
            DrawGizmos();
        }
    }

    void DrawGizmos()
    {
        Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
        Gizmos.DrawSphere(transform.position, spawnRadius);
    }
}