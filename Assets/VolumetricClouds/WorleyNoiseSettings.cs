using UnityEngine;

[CreateAssetMenu(fileName = "WorleyNoiseSettings", menuName = "Volumetric Clouds/Worley Noise Settings")]
public class WorleyNoiseSettings : ScriptableObject
{
    [Header("Texture Settings")]
    [Range(32, 256)]
    public int resolution = 128;
    
    [Header("Noise Parameters")]
    [Range(2, 16)]
    public int numCells = 4;
    
    [Range(1, 6)]
    public int octaves = 4;
    
    [Range(0.1f, 0.9f)]
    public float persistence = 0.5f;
    
    public int seed = 42;
    
    [Header("Generation Mode")]
    public bool useMultiOctave = true;
}