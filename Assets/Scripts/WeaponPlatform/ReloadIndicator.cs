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
                color = _outOfTraverseColor;
            }
            else if (_gun.ReadyToFire)
            {
                color = _readyColor;
            }
        }
        else if (_gun.ReadyToFire)
        {
            color = _readyColor;
        }
        
        ReloadCircleMaterial.SetColor(_colorID, color);
    }
    
    private void OnDestroy()
    {
        if (ReloadCircleMaterial != null)
            Destroy(ReloadCircleMaterial);
    }
}