using UnityEngine;
using UnityEngine.UI;

public class ReloadIndicator : MonoBehaviour
{
    public Shader ReloadCircleShader;
    public Material ReloadCircleMaterial;
    private Gun _gun;
    
    // Shader property IDs (cache for performance)
    private static readonly int _fillAmountID = Shader.PropertyToID("_FillAmount");
    private static readonly int _colorID = Shader.PropertyToID("_Color");
    
    [Header("Colors")]
    [SerializeField] private Color _readyColor = Color.green;
    [SerializeField] private Color _readyButNotAimedColor = new Color(0f, 0.5f, 0f, 1f); // Dark green
    [SerializeField] private Color _reloadingColor = Color.yellow;
    [SerializeField] private Color _outOfTraverseColor = Color.red;
    
    public void Initialize(Gun gun)
    {
        _gun = gun;
        ReloadCircleMaterial = new Material(ReloadCircleShader);
    }
    
    private void Update()
    {
        if (_gun == null || ReloadCircleMaterial == null) return;
        
        // Calculate reload progress
        float timeSinceLastShot = Time.time - _gun.LastShotTime;
        float reloadProgress = Mathf.Clamp01(timeSinceLastShot / _gun.FireDelay);
        
        // Set fill amount
        ReloadCircleMaterial.SetFloat(_fillAmountID, reloadProgress);
        
        // Determine color
        Color color = _reloadingColor;
        
        if (_gun.IsManualMode && _gun.ManualAimPosition != Vector3.zero)
        {
            if (!_gun.IsTargetWithinTraverseLimits(_gun.ManualAimPosition))
            {
                // Target is outside traverse limits
                color = _outOfTraverseColor;
            }
            else if (_gun.ReadyToFire)
            {
                // Ready to fire, check if aimed
                if (IsGunAimedAtTarget())
                {
                    color = _readyColor;
                }
                else
                {
                    color = _readyButNotAimedColor;
                }
            }
        }
        else if (_gun.ReadyToFire)
        {
            // Automatic mode
            if (_gun.IsAimed)
            {
                color = _readyColor;
            }
            else
            {
                color = _readyButNotAimedColor;
            }
        }
        
        ReloadCircleMaterial.SetColor(_colorID, color);
    }
    
    /// <summary>
    /// Check if the gun is currently aimed at the target position
    /// </summary>
    private bool IsGunAimedAtTarget()
    {
        if (_gun.ManualAimPosition == Vector3.zero)
            return false;
        
        float angleToTarget = _gun.GetTurretAngleToTarget(_gun.ManualAimPosition);
        return angleToTarget < _gun.AimedThreshold;
    }
    
    private void OnDestroy()
    {
        if (ReloadCircleMaterial != null)
            Destroy(ReloadCircleMaterial);
    }
}