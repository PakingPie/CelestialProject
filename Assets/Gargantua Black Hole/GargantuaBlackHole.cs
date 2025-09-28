using UnityEngine;

public class GargantuaBlackHole : MonoBehaviour
{
    private RenderTexture _garganturaPrev;
    private RenderTexture _garganturaCurr;
    public Material GargantuaBaseMaterial;
    public Material GargantuaFirstBloomMaterial;

    public bool TemporalAA = false;

    public void Init()
    {
        _garganturaPrev = new RenderTexture(512, 512, 24);
        _garganturaCurr = new RenderTexture(512, 512, 24);
    }

    public void Render()
    {
        if (_garganturaCurr == null && _garganturaPrev == null)
        {
            Init();
        }

        if (TemporalAA)
        {
            if (_garganturaPrev == null)
            {
                Init();
            }
            GargantuaBaseMaterial.EnableKeyword("TEMPORTAL_AA");
            Graphics.Blit(_garganturaPrev, _garganturaPrev, GargantuaBaseMaterial);
            GargantuaBaseMaterial.SetTexture("_MainTex", _garganturaPrev);
        }
        else
        {
            GargantuaBaseMaterial.DisableKeyword("TEMPORTAL_AA");
        }

        // Graphics.Blit(_garganturaPrev, Camera.main.activeTexture, GargantuaBaseMaterial);

        GargantuaFirstBloomMaterial.SetTexture("_GargantuaTex", _garganturaPrev);

    }

    void Update()
    {
        if (TemporalAA)
        {
            if (_garganturaPrev == null)
            {
                Init();
            }
            GargantuaBaseMaterial.EnableKeyword("TEMPORTAL_AA");
            Graphics.Blit(_garganturaPrev, _garganturaPrev, GargantuaBaseMaterial);
            GargantuaBaseMaterial.SetTexture("_MainTex", _garganturaPrev);
        }
        else
        {
            GargantuaBaseMaterial.DisableKeyword("TEMPORTAL_AA");
        }

        Graphics.Blit(null, _garganturaCurr, GargantuaBaseMaterial);


    }

}