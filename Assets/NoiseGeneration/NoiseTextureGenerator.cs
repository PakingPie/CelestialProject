// #define RGBA_NOISE

using System;
using UnityEngine;
using Random = UnityEngine.Random;


public class NoiseTextureGenerator : MonoBehaviour
{
    [Tooltip("Dimension of the noise texture to generate.")]
    [Range(8, 1024)]
    public int textureSize = 512;
    public enum NoiseTextureDimension { Noise2D, Noise3D }
    public string SavePath = "Assets/Textures/";
    private Texture2D _noiseTexture2D;
    private Texture3D _noiseTexture3D;

    public Texture2D NoiseTexture2D => _noiseTexture2D;
    public Texture3D NoiseTexture3D => _noiseTexture3D;

    // Conitional Enable for RGBA Noise
    

    public int Octaves = 4;
    [Range(0, 1)]
    public float Persistence = 0.5f;
    public float Lacunarity = 2.0f;
    public float Scale = 1.0f;

    public int Seed = 0;

    public void Generate2DNoiseTexture()
    {
        _noiseTexture2D = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float r = Random.Range(0.0f, 1.0f);
                float g = Random.Range(0.0f, 1.0f);
                float b = Random.Range(0.0f, 1.0f);
                Color color = new Color(r, g, b);
                _noiseTexture2D.SetPixel(x, y, color);
            }
        }
        _noiseTexture2D.Apply();
    }

    public void Generate2DPerlinNoiseTexture()
    {
        _noiseTexture2D = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

        System.Random rand = new System.Random(Seed);
        Vector2[] octaveOffsets = new Vector2[Octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1.0f;
        float frequency = 1.0f;

        for (int i = 0; i < Octaves; i++)
        {
            float offsetX = rand.Next(-100000, 100000);
            float offsetY = rand.Next(-100000, 100000);
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= Persistence;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;
        int halfSize = textureSize / 2;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                amplitude = 1;
                frequency = 1;


#if RGBA_NOISE
                float r = 0, g = 0, b = 0, a = 0;
                for (int i = 0; i < Octaves; i++)
                {
                    float sampleX = (x - halfSize + octaveOffsets[i].x) / Scale * frequency;
                    float sampleY = (y - halfSize + octaveOffsets[i].y) / Scale * frequency;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;

                    if (i % 4 == 0) r += (perlinValue + 1) * amplitude;
                    else if (i % 4 == 1) g += (perlinValue + 1) * amplitude;
                    else if (i % 4 == 2) b += (perlinValue + 1) * amplitude;

                    a += (perlinValue + 1) / 2 * amplitude;

                    amplitude *= Persistence;
                    frequency *= Lacunarity;
                }

                r = Mathf.Clamp01(r / maxPossibleHeight);
                g = Mathf.Clamp01(g / maxPossibleHeight);
                b = Mathf.Clamp01(b / maxPossibleHeight);
                a = Mathf.Clamp01(a / maxPossibleHeight);

                Color color = new Color(r, g, b, a);
#else
                float noiseHeight = 0;
                for (int i = 0; i < Octaves; i++)
                {
                    float sampleX = (x - halfSize + octaveOffsets[i].x) / Scale * frequency;
                    float sampleY = (y - halfSize + octaveOffsets[i].y) / Scale * frequency;
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= Persistence;
                    frequency *= Lacunarity;
                }

                if (noiseHeight > maxLocalNoiseHeight)
                {
                    maxLocalNoiseHeight = noiseHeight;
                }
                if (noiseHeight < minLocalNoiseHeight)
                {
                    minLocalNoiseHeight = noiseHeight;
                }

                noiseHeight = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseHeight);

                Color color = new Color(noiseHeight, noiseHeight, noiseHeight, noiseHeight);
#endif
                _noiseTexture2D.SetPixel(x, y, color);
            }
        }
        _noiseTexture2D.Apply();
        Debug.Log("Generated 2D Noise Texture");
    }

    public void Generate3DNoiseTexture()
    {
        _noiseTexture3D = new Texture3D(textureSize, textureSize, textureSize, TextureFormat.RGBA32, false);
        Color[] colors = new Color[textureSize * textureSize * textureSize];
        int index = 0;
        for (int z = 0; z < textureSize; z++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float r = Random.Range(0.0f, 1.0f);
                    float g = Random.Range(0.0f, 1.0f);
                    float b = Random.Range(0.0f, 1.0f);
                    float a = Random.Range(0.0f, 1.0f);
                    colors[index++] = new Color(r, g, b, a);
                }
            }
        }
        _noiseTexture3D.SetPixels(colors);
        _noiseTexture3D.Apply();
        Debug.Log("Generated 3D Noise Texture");
    }

    public void SaveTextureAsAsset(Texture texture, string saveName)
    {
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.CreateAsset(texture, SavePath + saveName + textureSize + ".asset");
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        Debug.Log($"Saved texture asset at: {SavePath}");
    }
}