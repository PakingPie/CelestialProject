using UnityEngine;


public class MissilePod : MonoBehaviour
{
    private AALauncher _launcher;
    private Transform _launchTransform;

    public float FireInterval = 2.0f;

    public Transform TargetPoint;

    private float _startTime;

    void Start()
    {
        _startTime = Time.time;

        _launcher = GetComponentInChildren<AALauncher>();
        _launchTransform = _launcher.GetComponent<Transform>();
    }

    void Update()
    {
        if (TargetPoint != null)
        {
            // Look at the target.
            _launchTransform.rotation = Quaternion.LookRotation(TargetPoint.position - transform.position, Vector3.up);

            if (Time.time - _startTime > FireInterval)
                _launcher.Launch(TargetPoint);
        }
        // If no target, just rotate slowly.
        else
        {
            _launchTransform.Rotate(Vector3.up, 10.0f * Time.deltaTime);
        }
    }
}