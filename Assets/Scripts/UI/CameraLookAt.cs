using UnityEngine;

public class CameraLookAt : MonoBehaviour
{
    public Transform target;

    private void Update()
    {
        if (target != null)
        {
            transform.LookAt(target);
        }
    }
}