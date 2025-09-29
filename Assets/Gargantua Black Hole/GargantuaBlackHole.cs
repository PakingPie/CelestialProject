using UnityEngine;

public class GargantuaBlackHole : MonoBehaviour
{
    private RenderTexture _garganturaPrev;
    private RenderTexture _garganturaCurr;
    public Material GargantuaBaseMaterial;

    public bool TemporalAA = false;

    public Material TestMat;

    public void SetTexture(Material mat, RenderTexture rt)
    {
        mat.SetTexture("_MainTex", rt);
    }

    public void Init()
    {
        _garganturaPrev = new RenderTexture(512, 512, 24);
        _garganturaCurr = new RenderTexture(512, 512, 24);
    }

    public void Render()
    {
        // _garganturaCurr.DiscardContents();
        // _garganturaPrev.DiscardContents();
        // if (_garganturaCurr == null && _garganturaPrev == null)
        // {
        Init();
        // }

        if (TemporalAA)
        {
            GargantuaBaseMaterial.EnableKeyword("TEMPORTAL_AA");
            Graphics.Blit(_garganturaPrev, _garganturaPrev, GargantuaBaseMaterial);
            GargantuaBaseMaterial.SetTexture("_MainTex", _garganturaPrev);
        }
        else
        {
            GargantuaBaseMaterial.DisableKeyword("TEMPORTAL_AA");
        }


        GargantuaBaseMaterial.SetTexture("_GargantuaTex", _garganturaCurr);
        Graphics.Blit(_garganturaPrev, _garganturaCurr, GargantuaBaseMaterial, pass: 1);
        

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
            Graphics.Blit(_garganturaPrev, _garganturaPrev, GargantuaBaseMaterial, pass: 0);
            GargantuaBaseMaterial.SetTexture("_MainTex", _garganturaPrev);
        }
        else
        {
            GargantuaBaseMaterial.DisableKeyword("TEMPORTAL_AA");
        }

        Graphics.Blit(_garganturaPrev, _garganturaCurr);
        GargantuaBaseMaterial.SetTexture("_GargantuaTex", _garganturaCurr);



    }

}