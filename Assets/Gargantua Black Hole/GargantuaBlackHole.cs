using UnityEngine;

public class GargantuaBlackHole : MonoBehaviour
{
    private RenderTexture _gargantuaPrev;
    private RenderTexture _gargantuaCurr;
    private RenderTexture _garganturaBlurred;
    public Material GargantuaBaseMaterial;
    public Material GargantuaFinalMaterial;

    public bool TemporalAA = false;

    public Material TestMat;

    public void SetTexture(Material mat, RenderTexture rt)
    {
        mat.SetTexture("_GargantuaTex", rt);
    }

    public void Init()
    {
        _gargantuaPrev = new RenderTexture(512, 512, 24);
        _gargantuaCurr = new RenderTexture(512, 512, 24);
        _garganturaBlurred = new RenderTexture(512, 512, 24);
    }



    public void Render()
    {
        // _gargantuaCurr.DiscardContents();
        // _garganturaPrev.DiscardContents();
        // if (_gargantuaCurr == null && _garganturaPrev == null)
        // {
        Init();
        // }

        if (TemporalAA)
        {
            Camera.main.targetTexture = _gargantuaPrev;
            Camera.main.RenderWithShader(GargantuaBaseMaterial.shader, "");
            Graphics.Blit(Camera.main.targetTexture, _gargantuaPrev);

            GargantuaBaseMaterial.EnableKeyword("TEMPORTAL_AA");
            GargantuaBaseMaterial.SetTexture("_GargantuaPrevTex", _gargantuaPrev);
            Graphics.Blit(_gargantuaCurr, _gargantuaCurr, GargantuaBaseMaterial, pass: 1);
        }
        else
        {
            Camera.main.targetTexture = _gargantuaCurr;
            Camera.main.RenderWithShader(GargantuaBaseMaterial.shader, "");
            Graphics.Blit(Camera.main.targetTexture, _gargantuaCurr);
            GargantuaBaseMaterial.DisableKeyword("TEMPORTAL_AA");
            Graphics.Blit(null, _gargantuaCurr, GargantuaBaseMaterial, pass: 1);
        }

        GargantuaBaseMaterial.SetTexture("_GargantuaTex", _gargantuaCurr);
        GargantuaBaseMaterial.SetTexture("_GarganturaBlurred", _garganturaBlurred);

        Graphics.Blit(_gargantuaCurr, _garganturaBlurred);
        Graphics.Blit(_garganturaBlurred, _garganturaBlurred, GargantuaBaseMaterial, pass: 2);
        Graphics.Blit(_garganturaBlurred, _garganturaBlurred, GargantuaBaseMaterial, pass: 3);

        // GargantuaFinalMaterial.SetTexture("_GargantuaTex", _gargantuaCurr);
        // GargantuaFinalMaterial.SetTexture("_GarganturaBlurred", _garganturaBlurred);

        // Graphics.Blit(_garganturaBlurred, _garganturaBlurred, GargantuaBaseMaterial, pass: 2);

        // Pass:3 is final composite
        // Graphics.Blit(_gargantuaCurr, _gargantuaCurr, GargantuaBaseMaterial, pass: 3);
        SetTexture(TestMat, _gargantuaCurr);


    }

    void Update()
    {



    }

}