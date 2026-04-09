// NebulaVolume.cs
// Place on a GameObject whose Transform defines the nebula's position, orientation,
// and overall size. The render feature discovers all active NebulaVolumes each frame.
// The local-space cube is ±0.5 — a default Unity cube works directly.

using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class NebulaVolume : MonoBehaviour
{
    // ─── Static registry ───
    public static readonly List<NebulaVolume> activeVolumes = new List<NebulaVolume>();

    // ─── Noise ───
    [Header("Noise (baked 3D texture)")]
    [Tooltip("Assign the Texture3D created by NebulaNoiseBaker.")]
    public Texture3D noiseTexture;

    [Tooltip("Must match the domainHalf used when baking the noise.")]
    public float noiseDomainHalf = 2.5f;

    // ─── Lighting ───
    [Header("Lighting")]
    public float lightPower = 200f;

    [ColorUsage(true, true)]
    public Color nebulaColor = Color.white;

    // ─── Emission ───
    [Header("Self-Emission")]
    [Tooltip("Strength of density-dependent self-emission (ionized gas glow).")]
    [Range(0f, 5f)] public float emissionStrength = 1.0f;

    // ─── Density Color Gradient ───
    [Header("Density Color Gradient")]
    [Tooltip("Color in low-density (ionized/hot) regions.")]
    [ColorUsage(true, true)]
    public Color colorLowDensity = new Color(0.3f, 0.6f, 0.9f, 1f);

    [Tooltip("Color in mid-density regions.")]
    [ColorUsage(true, true)]
    public Color colorMidDensity = new Color(0.85f, 0.25f, 0.5f, 1f);

    [Tooltip("Color in high-density (dust) regions.")]
    [ColorUsage(true, true)]
    public Color colorHighDensity = new Color(0.6f, 0.3f, 0.1f, 1f);

    // ─── Detail Noise ───
    [Header("Detail Noise")]
    [Tooltip("How much the mid-frequency channel erodes the base density.")]
    [Range(0f, 1f)] public float detailStrength = 0.35f;

    [Tooltip("How much the cellular channel carves voids.")]
    [Range(0f, 1f)] public float voidStrength = 0.4f;

    [Tooltip("Contrast exponent applied to final density. Higher = sharper edges.")]
    [Range(0.5f, 4f)] public float densityContrast = 1.0f;

    // ─── Shape ───
    [Header("Shape")]
    [Range(0f, 1f)]   public float   fadeInnerRadius      = 0.55f;
    [Range(0f, 1f)]   public float   fadeOuterRadius       = 0.95f;
    [Range(0f, 2f)]   public float   fadeNoiseStrength     = 0.50f;
    [Range(0f, 0.5f)] public float   fadeBoxMargin         = 0.20f;
    [Range(0.1f, 5f)] public float   shapeNoiseScale       = 1.50f;
    [Range(0f, 1f)]   public float   shapeTendrilStrength  = 0.45f;
    public Vector3 axisStretch = new Vector3(1f, 0.6f, 1.4f);

    // ─── Quality ───
    [Header("Quality")]
    [Range(8, 64)]  public int stepsPrimary = 32;
    [Range(2, 16)]  public int stepsLight   = 8;

    // ─── Stars ───
    [Header("Stars")]
    public bool enableStars = true;
    [Range(0f, 1f)]   public float starDensity   = 0.55f;
    [Range(0f, 0.5f)] public float starBrightness = 0.05f;

    // ─── Dithering ───
    [Header("Dithering")]
    [Tooltip("1024×1024 blue noise texture (Repeat wrap, Point filter).")]
    public Texture2D blueNoiseTexture;
    [Range(0f, 1f)] public float ditherSpeed = 0.1f;

    // ─── Lifecycle ───

    void OnEnable()
    {
        if (!activeVolumes.Contains(this))
            activeVolumes.Add(this);
    }

    void OnDisable()
    {
        activeVolumes.Remove(this);
    }
}