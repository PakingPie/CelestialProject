using UnityEngine;


public class MissilePod : WeaponBase
{
    private AALauncher _launcher;
    private Transform _launchTransform;

    public float FireInterval = 2.0f;

    private float _startTime;

    void Start()
    {
        _startTime = Time.time;

        _launcher = GetComponentInChildren<AALauncher>();
        _launchTransform = _launcher.GetComponent<Transform>();
    }

    void Update()
    {
        
    }
}