using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class ShieldHitEffect : MonoBehaviour
{
    public readonly struct ShieldImpactData
    {
        public readonly Vector3 WorldImpactPoint;
        public readonly Vector3 IncomingDirection;
        public readonly bool HasIncomingDirection;

        public ShieldImpactData(Vector3 worldImpactPoint)
        {
            WorldImpactPoint = worldImpactPoint;
            IncomingDirection = Vector3.zero;
            HasIncomingDirection = false;
        }

        public ShieldImpactData(Vector3 worldImpactPoint, Vector3 incomingDirection)
        {
            WorldImpactPoint = worldImpactPoint;

            if (incomingDirection.sqrMagnitude > 1e-6f)
            {
                IncomingDirection = incomingDirection.normalized;
                HasIncomingDirection = true;
            }
            else
            {
                IncomingDirection = Vector3.zero;
                HasIncomingDirection = false;
            }
        }
    }

    private static Texture2D s_blackTex;
    private const float UVWrapHalfRange = 0.5f;
    private const float MinDirectionSqrMagnitude = 1e-6f;

    public GameObject ShieldGO;
    [Header("Settings")]
    public int TextureSize = 64;
    public float HitImpactScale = 0.1f;
    public float HitImpactDuration = 1.0f;
    [Range(1, 128)]
    public int MaxActiveRipples = 32;
    [Range(0.0f, 0.05f)]
    public float MergeUvDistanceThreshold = 0.01f;
    [Range(0.0f, 0.1f)]
    public float MergeTimeThreshold = 0.02f;

    [Header("Decay Settings")]
    [Range(0.1f, 0.99f)]
    public float DecayPerSecond = 0.75f;
    [Range(0.0f, 0.1f)]
    public float MinDecayThreshold = 0.02f;
    public float ForceClearInterval = 3.0f;

    [Header("Shaders")]
    public Shader HitEffectShader;
    public Shader CumulativeShader;

    [Header("Debug")]
    public bool EnableDebugMode = false;
    public GameObject DebugQuad;

    private RenderTexture _cumulativeRT;
    private RenderTexture _singleEffectRT;
    private RenderTexture _tempRT;

    private Texture2D _blackTex;

    private Material _hitEffectMat;
    private Material _cumulativeMat;

    private MaterialPropertyBlock _shieldPropBlock;
    private MeshRenderer _shieldRenderer;
    private Collider _shieldCollider;
    private MeshFilter _shieldMeshFilter;

    private List<ActiveRipple> _activeRipples = new List<ActiveRipple>();
    private bool _isInitialized = false;
    private float _forceClearTimer = 0f;

    private class ActiveRipple
    {
        public Vector2 uv;
        public float timer;
    }

    private Transform ShieldTransform => ShieldGO != null ? ShieldGO.transform : transform;
    private Vector3 ShieldLocalCenter => _shieldMeshFilter != null && _shieldMeshFilter.sharedMesh != null
        ? _shieldMeshFilter.sharedMesh.bounds.center
        : Vector3.zero;


    private RenderTexture CreateRT()
    {
        RenderTexture rt = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        return rt;
    }

    public void Init()
    {
        if (_isInitialized)
            return;

        if (_cumulativeRT == null) _cumulativeRT = CreateRT();
        if (_singleEffectRT == null) _singleEffectRT = CreateRT();
        if (_tempRT == null) _tempRT = CreateRT();
        if (s_blackTex == null)
        {
            s_blackTex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true)
            {
                name = "ShieldHitEffect BlackTex"
            };
            s_blackTex.SetPixels(new Color[] { Color.black, Color.black, Color.black, Color.black });
            s_blackTex.Apply();
        }

        _blackTex = s_blackTex;

        if (_cumulativeMat == null) _cumulativeMat = new Material(CumulativeShader);

        if (_hitEffectMat == null)
        {
            _hitEffectMat = new Material(HitEffectShader);

            _hitEffectMat.SetFloat("_EdgeMax", 0.05f);
            _hitEffectMat.SetFloat("_Thickness", 0.01f);
            _hitEffectMat.DisableKeyword("_CIRCLE_FILL");
            _hitEffectMat.DisableKeyword("_CIRCLE_FILL_SDF");
            _hitEffectMat.EnableKeyword("_CIRCLE_STROKE");
            _hitEffectMat.DisableKeyword("_CIRCLE_STROKE_SDF");
        }

        if (_shieldPropBlock == null) _shieldPropBlock = new MaterialPropertyBlock();
        if (ShieldGO != null)
        {
            _shieldRenderer = ShieldGO.GetComponent<MeshRenderer>();
            _shieldCollider = ShieldGO.GetComponent<Collider>();
            _shieldMeshFilter = ShieldGO.GetComponent<MeshFilter>();
        }

        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);

        _isInitialized = true;
        enabled = false;
    }

    public void ClearAll()
    {
        ReleaseRT(ref _cumulativeRT);
        ReleaseRT(ref _singleEffectRT);
        ReleaseRT(ref _tempRT);

        if (_hitEffectMat != null) Destroy(_hitEffectMat);
        if (_cumulativeMat != null) Destroy(_cumulativeMat);

        _hitEffectMat = null;
        _cumulativeMat = null;
        _shieldRenderer = null;
        _shieldCollider = null;
        _shieldMeshFilter = null;
        _shieldPropBlock = null;
        _blackTex = null;
        _activeRipples.Clear();
        _forceClearTimer = 0f;

        _isInitialized = false;
    }

    private void ReleaseRT(ref RenderTexture rt)
    {
        if (rt != null)
        {
            if (RenderTexture.active == rt)
                RenderTexture.active = null;
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }

    public void RegisterImpact(ShieldImpactData impact)
    {
        // if (!_isInitialized) Init();

        if (!TryProjectImpactToUv(impact, out Vector2 uv))
            return;

        RegisterImpactUv(uv);
    }

    // Debug-only path when a true shield-surface raycast hit is already available.
    public void GetHit(RaycastHit hit)
    {
        RegisterImpactUv(hit.textureCoord);
    }

    private void RegisterImpactUv(Vector2 uv)
    {
        if (!_isInitialized) Init();

        if (TryMergeRipple(uv))
        {
            _forceClearTimer = 0f;
            enabled = true;
            return;
        }

        EnforceRippleCap();

        _activeRipples.Add(new ActiveRipple
        {
            uv = uv,
            timer = 0.0f
        });

        _forceClearTimer = 0f;
        enabled = true;
    }

    private bool TryProjectImpactToUv(ShieldImpactData impact, out Vector2 uv)
    {
        Transform shieldTransform = ShieldTransform;
        Vector3 localImpactPoint = shieldTransform.InverseTransformPoint(impact.WorldImpactPoint) - ShieldLocalCenter;

        bool hasPointDirection = localImpactPoint.sqrMagnitude > MinDirectionSqrMagnitude;
        Vector3 pointDirection = hasPointDirection ? localImpactPoint.normalized : Vector3.zero;
        Vector3 resolvedDirection = pointDirection;

        if (impact.HasIncomingDirection)
        {
            Vector3 incomingLocal = shieldTransform.InverseTransformDirection(impact.IncomingDirection);
            if (incomingLocal.sqrMagnitude > MinDirectionSqrMagnitude)
            {
                Vector3 desiredHemisphereNormal = -incomingLocal.normalized;

                if (!hasPointDirection)
                {
                    resolvedDirection = desiredHemisphereNormal;
                }
                else if (Vector3.Dot(pointDirection, desiredHemisphereNormal) < 0f)
                {
                    resolvedDirection = Vector3.Reflect(pointDirection, desiredHemisphereNormal);
                }
            }
        }

        if (resolvedDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            uv = default;
            return false;
        }

        return TryResolveUvFromCollider(resolvedDirection.normalized, out uv)
            || TryResolveUvAnalytically(resolvedDirection.normalized, out uv);
    }

    private bool TryResolveUvFromCollider(Vector3 localDirection, out Vector2 uv)
    {
        if (_shieldCollider == null)
        {
            uv = default;
            return false;
        }

        Vector3 worldDirection = ShieldTransform.TransformDirection(localDirection).normalized;
        if (worldDirection.sqrMagnitude <= MinDirectionSqrMagnitude)
        {
            uv = default;
            return false;
        }

        Bounds bounds = _shieldCollider.bounds;
        float rayOffset = Mathf.Max(bounds.extents.magnitude * 2f, 0.1f);
        Vector3 worldCenter = ShieldTransform.TransformPoint(ShieldLocalCenter);
        Vector3 rayOrigin = worldCenter + worldDirection * rayOffset;
        Ray ray = new Ray(rayOrigin, -worldDirection);

        if (_shieldCollider.Raycast(ray, out RaycastHit hit, rayOffset * 2f))
        {
            uv = hit.textureCoord;
            return true;
        }

        uv = default;
        return false;
    }

    private static bool TryResolveUvAnalytically(Vector3 localDirection, out Vector2 uv)
    {
        uv = DirectionToUv(localDirection);
        return true;
    }

    private static Vector2 DirectionToUv(Vector3 localDirection)
    {
        float u = Mathf.Atan2(localDirection.z, localDirection.x) / (2f * Mathf.PI) + 0.5f;
        float v = Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) / Mathf.PI + 0.5f;
        return new Vector2(u, v);
    }

    private bool TryMergeRipple(Vector2 uv)
    {
        float mergeDistanceSqr = MergeUvDistanceThreshold * MergeUvDistanceThreshold;

        for (int i = 0; i < _activeRipples.Count; i++)
        {
            ActiveRipple ripple = _activeRipples[i];
            if (ripple.timer > MergeTimeThreshold)
                continue;

            if (WrappedUvDistanceSqr(ripple.uv, uv) > mergeDistanceSqr)
                continue;

            ripple.uv = uv;
            ripple.timer = 0.0f;
            return true;
        }

        return false;
    }

    private void EnforceRippleCap()
    {
        if (MaxActiveRipples < 1)
            MaxActiveRipples = 1;

        if (_activeRipples.Count < MaxActiveRipples)
            return;

        int oldestIndex = 0;
        float oldestTimer = float.MinValue;
        for (int i = 0; i < _activeRipples.Count; i++)
        {
            if (_activeRipples[i].timer <= oldestTimer)
                continue;

            oldestTimer = _activeRipples[i].timer;
            oldestIndex = i;
        }

        _activeRipples.RemoveAt(oldestIndex);
    }

    private static float WrappedUvDistanceSqr(Vector2 a, Vector2 b)
    {
        float du = Mathf.Abs(a.x - b.x);
        if (du > UVWrapHalfRange)
            du = 1f - du;

        float dv = Mathf.Abs(a.y - b.y);
        return du * du + dv * dv;
    }



    void Update()
    {
        if (!_isInitialized || _cumulativeMat == null || _hitEffectMat == null) return;

        if (_activeRipples.Count == 0)
        {
            _forceClearTimer += Time.deltaTime;
            if (_forceClearTimer > ForceClearInterval)
            {
                if (_forceClearTimer < ForceClearInterval + 0.1f)
                {
                    Graphics.Blit(_blackTex, _cumulativeRT);
                }
                enabled = false;
                return;
            }
        }
        else
        {
            _forceClearTimer = 0f;
        }

        // Each frame, darken the entire texture slightly.
        // Here, _HitTex is set to a black texture, indicating no new additions, only decay.
        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
        _cumulativeMat.SetTexture("_HitTex", _blackTex);

        float frameDecay = Mathf.Pow(DecayPerSecond, Time.deltaTime * 60f);
        _cumulativeMat.SetFloat("_Decay", frameDecay);
        _cumulativeMat.SetFloat("_MinThreshold", MinDecayThreshold);

        Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
        Graphics.Blit(_tempRT, _cumulativeRT); // Swap back to _cumulativeRT
        // --- Step 2: Process and blend all active ripples ---
        // Iterate in reverse order for safe removal
        for (int i = _activeRipples.Count - 1; i >= 0; i--)
        {
            ActiveRipple ripple = _activeRipples[i];
            ripple.timer += Time.deltaTime;

            // If the ripple duration has ended, remove it from the list
            if (ripple.timer > HitImpactDuration)
            {
                _activeRipples.RemoveAt(i);
                continue;
            }

            // Calculate the current state of the ripple
            float progress = ripple.timer / HitImpactDuration;
            float currentSize = HitImpactScale * progress;
            float currentFade = 1.0f - progress;

            // 2.1 Draw single ripple to _singleEffectRT
            _hitEffectMat.SetVector("_HitUV", ripple.uv);
            _hitEffectMat.SetFloat("_Size", currentSize);
            _hitEffectMat.SetFloat("_Fade", currentFade);

            // This step generates a black background on _singleEffectRT, with only the current white circle
            Graphics.Blit(null, _singleEffectRT, _hitEffectMat);

            // 2.2 Blend the single ripple onto the cumulative texture
            // Key: Here _Decay is set to 1.0 because we don't want the old texture to darken again (step 1 already darkened it)
            // We only want to max blend the new white circle.
            _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
            _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);
            _cumulativeMat.SetFloat("_Decay", 1.0f);
            _cumulativeMat.SetFloat("_MinThreshold", 0.0f);

            Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
            Graphics.Blit(_tempRT, _cumulativeRT);
        }

        if (_shieldRenderer != null)
        {
            _shieldRenderer.GetPropertyBlock(_shieldPropBlock);
            _shieldPropBlock.SetTexture("_HitAreaTex", _cumulativeRT);
            _shieldRenderer.SetPropertyBlock(_shieldPropBlock);
        }
    }

    // void Update()
    // {
    //     if (!_isInitialized || _cumulativeMat == null || _hitEffectMat == null) return;
    //     // Debug.Log("Active Ripples Count: " + _activeRipples.Count);
    //     // --- Step 1: Global Decay ---
    //     // Each frame, darken the entire texture slightly.
    //     // Here, _HitTex is set to a black texture, indicating no new additions, only decay.
    //     _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
    //     _cumulativeMat.SetTexture("_HitTex", _blackTex);

    //     Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
    //     Graphics.Blit(_tempRT, _cumulativeRT); // Swap back to _cumulativeRT

    //     // --- Step 2: Process and blend all active ripples ---
    //     // Iterate in reverse order for safe removal
    //     for (int i = _activeRipples.Count - 1; i >= 0; i--)
    //     {
    //         ActiveRipple ripple = _activeRipples[i];
    //         ripple.timer += Time.deltaTime;

    //         // If the ripple duration has ended, remove it from the list
    //         if (ripple.timer > HitImpactDuration)
    //         {
    //             _activeRipples.RemoveAt(i);
    //             continue;
    //         }

    //         // Calculate the current state of the ripple
    //         float progress = ripple.timer / HitImpactDuration;
    //         float currentSize = HitImpactScale * progress;
    //         float currentFade = 1.0f - progress;

    //         // 2.1 Draw single ripple to _singleEffectRT
    //         _hitEffectMat.SetVector("_HitUV", ripple.uv);
    //         _hitEffectMat.SetFloat("_Size", currentSize);
    //         _hitEffectMat.SetFloat("_Fade", currentFade);

    //         // This step generates a black background on _singleEffectRT, with only the current white circle
    //         Graphics.Blit(null, _singleEffectRT, _hitEffectMat);

    //         // 2.2 Blend the single ripple onto the cumulative texture
    //         // Key: Here _Decay is set to 1.0 because we don't want the old texture to darken again (step 1 already darkened it)
    //         // We only want to max blend the new white circle.
    //         _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
    //         _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);
    //         _cumulativeMat.SetFloat("_Decay", DecayPerSecond);

    //         Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
    //         Graphics.Blit(_tempRT, _cumulativeRT);
    //     }
    // }

    // Coroutine to animate a single hit effect over time. Not used in current implementation.
    IEnumerator AnimateSingleHit(Vector2 hitUV)
    {
        float timer = 0.0f;
        Material hitInstanceMat = new Material(HitEffectShader);
        hitInstanceMat.SetVector("_HitUV", hitUV);
        while (timer < HitImpactDuration)
        {
            timer += Time.deltaTime;
            float strength = Mathf.Lerp(HitImpactScale, 0.0f, timer / HitImpactDuration);
            strength = Mathf.Clamp(1 - strength, 0.0f, 1.0f);
            hitInstanceMat.SetFloat("_Size", strength);
            hitInstanceMat.SetFloat("_Fade", 1 - strength);

            Graphics.Blit(null, _singleEffectRT, hitInstanceMat, pass: 0);   // Get single hit effect

            RenderTexture tempRT = RenderTexture.GetTemporary(_cumulativeRT.descriptor);
            Graphics.Blit(_cumulativeRT, tempRT, _cumulativeMat, pass: 0);    // Get cumulative effect
            Graphics.Blit(tempRT, _cumulativeRT);                          // Copy back to cumulative RT
            RenderTexture.ReleaseTemporary(tempRT);

            ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HitAreaTex", _cumulativeRT);

            yield return null;
        }

        Destroy(hitInstanceMat);
    }

    void OnDestroy()
    {
        ClearAll();
    }

    void OnDisable()
    {
        _activeRipples.Clear();
        _forceClearTimer = 0f;
    }

    // void OnGUI()
    // {
    //     if (_cumulativeRT != null && Event.current.type == EventType.Repaint)
    //     {
    //         GUI.DrawTexture(new Rect(10, 10, 128, 128), _cumulativeRT);
    //         GUI.Label(new Rect(10, 145, 200, 20), $"Active Ripples: {_activeRipples.Count}");
    //         GUI.Label(new Rect(10, 165, 200, 20), $"Clear Timer: {_forceClearTimer:F2}s");
    //     }
    // }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ShieldHitEffect))]
public class ShieldHitEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ShieldHitEffect shieldHitEffect = (ShieldHitEffect)target;
        if(GUILayout.Button("Initialize"))
        {
            shieldHitEffect.Init();
        }

        if (GUILayout.Button("Clear All"))
        {
            shieldHitEffect.ClearAll();
        }
    }
}
#endif