using UnityEngine;

public class MoveAroundACenter : MonoBehaviour
{
    public Transform CenterPoint;
    public float Radius = 5f;
    public float Speed = 1f;

    private float _angle;

    void Update()
    {
        if (CenterPoint == null) return;

        _angle += Speed * Time.deltaTime;
        float x = CenterPoint.position.x + Radius * Mathf.Cos(_angle);
        float z = CenterPoint.position.z + Radius * Mathf.Sin(_angle);
        transform.position = new Vector3(x, transform.position.y, z);
        transform.LookAt(CenterPoint);
    }
}