using UnityEditor;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public enum GizmoType
    {
        Never, SelectedOnly, Always
    }

    public Boid[] prefabs;
    public float spawnRadius = 10.0f;
    public int spawnCount = 10;
    public Color color;
    public GizmoType showSpawnRegion;

    public Vector2 HeightRange = new Vector2(-1.0f, 1.0f);

    void Awake()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 randomSphere = Random.insideUnitSphere * spawnRadius;
            Vector3 pos = transform.position + new Vector3(randomSphere.x, Mathf.Clamp(randomSphere.y, HeightRange.x, HeightRange.y), randomSphere.z); // originly Random.insideUnitSphere
            Boid boid = Instantiate(prefabs[Random.Range(0, prefabs.Length)]);
            boid.transform.position = pos;
            randomSphere = Random.insideUnitSphere;
            boid.transform.forward = new Vector3(randomSphere.x, Mathf.Clamp(randomSphere.y, HeightRange.x, HeightRange.y), randomSphere.z); // originly Random.insideUnitSphere

            boid.SetColor(color);
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