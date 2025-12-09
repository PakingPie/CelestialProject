using UnityEngine;

[CreateAssetMenu(fileName = "IndicatorSettings", menuName = "Combat/Indicator Settings")]
public class PredictionIndicatorSettings : ScriptableObject
{
    [Header("Prefabs")]
    public GameObject PositionIndicatorPrefab;
    public GameObject LeadIndicatorPrefab;

    [Header("Colors")]
    public Color PositionColor = Color.red;
    public Color LeadColor = Color.yellow;
    public Color LineColor = Color.white;

    [Header("Display")]
    public bool ShowPositionIndicator = true;
    public bool ShowLeadIndicator = true;
    public bool ShowConnectingLine = true;

    [Header("Scaling")]
    public float BaseScale = 1f;
    public float MinScale = 0.3f;
    public float MaxScale = 2f;
    [Header("Distance-Based Color")]
    public bool UseDistanceColor = true;
    public Color NearColor = Color.red;      // Bright when close
    public Color FarColor = Color.darkRed;   // Dark when far
}