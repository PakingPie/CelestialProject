using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
public class AtmosphereSingle : MonoBehaviour
{
    public Material material;
	void Update()
	{
		Init();
	}
    void Init()
    {
        if (effectHolders == null || effectHolders.Count == 0 || !Application.isPlaying)
        {
            var generators = FindObjectsByType<CelestialBodyGenerator>(FindObjectsSortMode.None);
            effectHolders = new List<EffectHolder>(generators.Length);
            for (int i = 0; i < generators.Length; i++)
            {
                effectHolders.Add(new EffectHolder(generators[i]));
            }
        }

        if (effectHolders.Count > 0)
        {
            Camera cam = Camera.main;
            Vector3 camPos = cam.transform.position;

            SortFarToNear(camPos);

            for (int i = 0; i < effectHolders.Count; i++)
            {
                EffectHolder effectHolder = effectHolders[i];

                // Atmospheres
                if (effectHolder.atmosphereEffect != null)
                {
                    effectHolder.atmosphereEffect.UpdateSettings(effectHolder.generator, material);
                }

            }
        }
    }


    List<EffectHolder> effectHolders;
    List<float> sortDistances;

    public class EffectHolder
    {
        public CelestialBodyGenerator generator;
        // public OceanEffect oceanEffect;
        public AtmosphereEffect atmosphereEffect;

        public EffectHolder(CelestialBodyGenerator generator)
        {
            this.generator = generator;
            // if (generator.body.shading.hasOcean && generator.body.shading.oceanSettings)
            // {
            // 	oceanEffect = new OceanEffect();
            // }
            if (generator.body.shading.hasAtmosphere && generator.body.shading.atmosphereSettings)
            {
                atmosphereEffect = new AtmosphereEffect();
            }
        }

        public float DstFromSurface(Vector3 viewPos)
        {
            return Mathf.Max(0, (generator.transform.position - viewPos).magnitude - generator.BodyScale);
        }
    }

    void SortFarToNear(Vector3 viewPos)
    {
        for (int i = 0; i < effectHolders.Count; i++)
        {
            float dstToSurface = effectHolders[i].DstFromSurface(viewPos);
            sortDistances.Add(dstToSurface);
        }

        for (int i = 0; i < effectHolders.Count - 1; i++)
        {
            for (int j = i + 1; j > 0; j--)
            {
                if (sortDistances[j - 1] < sortDistances[j])
                {
                    float tempDst = sortDistances[j - 1];
                    var temp = effectHolders[j - 1];
                    sortDistances[j - 1] = sortDistances[j];
                    sortDistances[j] = tempDst;
                    effectHolders[j - 1] = effectHolders[j];
                    effectHolders[j] = temp;
                }
            }
        }
    }
}