using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class AsteroidPrefabBuilder : EditorWindow
{
    [SerializeField] private GameObject lod0Source;
    [SerializeField] private GameObject lod1Source;
    [SerializeField] private GameObject lod2Source;
    [SerializeField] private string outputFolder = "Assets/Prefabs/Asteroids";

    [Header("LOD Transition Distances")]
    [SerializeField] private float lod0ScreenHeight = 0.6f;
    [SerializeField] private float lod1ScreenHeight = 0.3f;
    [SerializeField] private float lod2ScreenHeight = 0.1f;

    private SerializedObject _serializedObject;

    [MenuItem("Tools/Asteroid Prefab Builder")]
    static void ShowWindow()
    {
        GetWindow<AsteroidPrefabBuilder>("Asteroid Prefab Builder");
    }

    private void OnEnable()
    {
        _serializedObject = new SerializedObject(this);
    }

    private void OnGUI()
    {
        _serializedObject.Update();

        EditorGUILayout.LabelField("OBJ Sources (drag imported OBJ root objects)", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        lod0Source = (GameObject)EditorGUILayout.ObjectField("LOD0 (High)", lod0Source, typeof(GameObject), false);
        lod1Source = (GameObject)EditorGUILayout.ObjectField("LOD1 (Medium)", lod1Source, typeof(GameObject), false);
        lod2Source = (GameObject)EditorGUILayout.ObjectField("LOD2 (Low)", lod2Source, typeof(GameObject), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("LOD Screen Heights", EditorStyles.boldLabel);
        lod0ScreenHeight = EditorGUILayout.Slider("LOD0 → LOD1", lod0ScreenHeight, 0.01f, 1f);
        lod1ScreenHeight = EditorGUILayout.Slider("LOD1 → LOD2", lod1ScreenHeight, 0.01f, 1f);
        lod2ScreenHeight = EditorGUILayout.Slider("LOD2 → Cull", lod2ScreenHeight, 0.001f, 0.5f);

        EditorGUILayout.Space(8);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space(12);

        GUI.enabled = lod0Source != null;
        if (GUILayout.Button("Build Asteroid Prefabs", GUILayout.Height(35)))
        {
            BuildPrefabs();
        }
        GUI.enabled = true;

        _serializedObject.ApplyModifiedProperties();
    }

    private void BuildPrefabs()
    {
        // Gather child meshes from each LOD source
        var lod0Children = GetMeshChildren(lod0Source);
        var lod1Children = lod1Source != null ? GetMeshChildren(lod1Source) : null;
        var lod2Children = lod2Source != null ? GetMeshChildren(lod2Source) : null;

        if (lod0Children.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "LOD0 source has no child meshes.", "OK");
            return;
        }

        // Ensure output folder exists
        EnsureFolderExists(outputFolder);

        int created = 0;

        for (int i = 0; i < lod0Children.Count; i++)
        {
            var (name0, mesh0, mat0) = lod0Children[i];

            // Try to find matching mesh in LOD1/LOD2 by index (same order from Blender export)
            (string name, Mesh mesh, Material mat)? lod1Match = lod1Children != null && i < lod1Children.Count ? lod1Children[i] : null;
            (string name, Mesh mesh, Material mat)? lod2Match = lod2Children != null && i < lod2Children.Count ? lod2Children[i] : null;

            string prefabName = SanitizeName(name0, i);
            GameObject prefab = BuildSinglePrefab(prefabName, (mesh0, mat0), lod1Match, lod2Match);

            string prefabPath = $"{outputFolder}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            DestroyImmediate(prefab);
            created++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"AsteroidPrefabBuilder: Created {created} prefab(s) in {outputFolder}");
        EditorUtility.DisplayDialog("Done", $"Created {created} asteroid prefab(s) in\n{outputFolder}", "OK");
    }

    private GameObject BuildSinglePrefab(string name,
        (Mesh mesh, Material mat) lod0,
        (string name, Mesh mesh, Material mat)? lod1,
        (string name, Mesh mesh, Material mat)? lod2)
    {
        GameObject root = new GameObject(name);

        List<LOD> lods = new List<LOD>();

        // LOD0
        GameObject lod0GO = CreateLODChild(root, "LOD0", lod0.mesh, lod0.mat);
        Renderer[] lod0Renderers = lod0GO.GetComponentsInChildren<Renderer>();
        lods.Add(new LOD(lod0ScreenHeight, lod0Renderers));

        // LOD1
        if (lod1.HasValue)
        {
            GameObject lod1GO = CreateLODChild(root, "LOD1", lod1.Value.mesh, lod1.Value.mat);
            Renderer[] lod1Renderers = lod1GO.GetComponentsInChildren<Renderer>();
            lods.Add(new LOD(lod1ScreenHeight, lod1Renderers));
        }

        // LOD2
        if (lod2.HasValue)
        {
            GameObject lod2GO = CreateLODChild(root, "LOD2", lod2.Value.mesh, lod2.Value.mat);
            Renderer[] lod2Renderers = lod2GO.GetComponentsInChildren<Renderer>();
            lods.Add(new LOD(lod2ScreenHeight, lod2Renderers));
        }

        // Add LOD Group
        if (lods.Count > 1)
        {
            LODGroup lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(lods.ToArray());
            lodGroup.RecalculateBounds();
        }

        return root;
    }

    private GameObject CreateLODChild(GameObject parent, string childName, Mesh mesh, Material material)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent.transform, false);

        MeshFilter mf = child.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = child.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;

        return child;
    }

    private List<(string name, Mesh mesh, Material mat)> GetMeshChildren(GameObject source)
    {
        var results = new List<(string, Mesh, Material)>();

        if (source == null) return results;

        // Check if the source itself has a mesh (single-mesh OBJ)
        MeshFilter rootMF = source.GetComponent<MeshFilter>();
        if (rootMF != null && rootMF.sharedMesh != null)
        {
            MeshRenderer rootMR = source.GetComponent<MeshRenderer>();
            Material mat = rootMR != null ? rootMR.sharedMaterial : null;
            results.Add((source.name, rootMF.sharedMesh, mat));
            return results;
        }

        // Otherwise, iterate children
        for (int i = 0; i < source.transform.childCount; i++)
        {
            Transform child = source.transform.GetChild(i);
            MeshFilter mf = child.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshRenderer mr = child.GetComponent<MeshRenderer>();
                Material mat = mr != null ? mr.sharedMaterial : null;
                results.Add((child.name, mf.sharedMesh, mat));
            }
        }

        return results;
    }

    private string SanitizeName(string rawName, int index)
    {
        // Try to extract a clean name like "Asteroid_1" from "Asteroid_1_Sphere.002"
        if (rawName.Contains("Asteroid"))
        {
            string[] parts = rawName.Split('_');
            if (parts.Length >= 2)
                return $"Asteroid_{parts[1]}";
        }
        return $"Asteroid_{index + 1}";
    }

    private void EnsureFolderExists(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
