using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor Window for creating Texture2DArrays from terrain layer folders.
/// Each subfolder should contain albedo, normal, and mask textures identified by naming.
/// </summary>
[System.Serializable]
public class TerrainLayerData
{
    [HorizontalGroup("GroupName"), LabelWidth(80)]
    public string LayerName = "Layer";

    [HorizontalGroup("Albedo"), LabelText("Albedo"), PreviewField(50), HideLabel]
    public Texture2D Albedo;

    [HorizontalGroup("Normal"), LabelText("Normal"), PreviewField(50), HideLabel]
    public Texture2D Normal;

    [HorizontalGroup("Mask"), LabelText("Mask"), PreviewField(50), HideLabel]
    public Texture2D Mask;
}

/// <summary>
/// Odin-based Editor Window to batch-create Texture2DArrays for terrain materials.
/// Supports Albedo, Normal, and Mask arrays from structured subfolders.
/// </summary>
public class ATTerrainTextureArrayCreator : OdinEditorWindow
{
    [MenuItem("Tools/AT Grass Cloud/Terrain Texture Array Creator")]
    private static void OpenWindow()
    {
        GetWindow<ATTerrainTextureArrayCreator>().Show();
    }

    [Header(" Input Settings")]
    [FolderPath]
    [BoxGroup(" Input Settings")]
    public string SourceFolder = "Assets/ATGrassCloud/Material/Terrain/SubMaterial";

    [BoxGroup(" Input Settings")]
    [Button(" Scan Subfolders")]
    [GUIColor(0.4f, 0.8f, 1.0f, 1.0f)]
    private void ScanSubfolders()
    {
        Layers.Clear();

        if (string.IsNullOrEmpty(SourceFolder) || !Directory.Exists(SourceFolder))
        {
            Debug.LogError($"Source folder not found: {SourceFolder}");
            return;
        }

        string[] subDirs = Directory.GetDirectories(SourceFolder);
        if (subDirs.Length == 0)
        {
            Debug.Log("No subfolders found in the source directory.");
            return;
        }

        foreach (string dirPath in subDirs)
        {
            string dirName = new DirectoryInfo(dirPath).Name;
            TerrainLayerData layer = new TerrainLayerData { LayerName = dirName };

            string[] files = Directory.GetFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (!ext.Equals(".png") && !ext.Equals(".jpg") && 
                    !ext.Equals(".tga") && !ext.Equals(".tif") && !ext.Equals(".jpeg"))
                    continue;

                string assetPath = file.Replace("\\", "/").Replace(Application.dataPath, "Assets");
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null) continue;

                string filenameLower = Path.GetFileNameWithoutExtension(file).ToLower();

                if (IsKeywordIn(filenameLower, "albedo", "diffuse", "basecolor", "color"))
                    layer.Albedo = tex;
                else if (IsKeywordIn(filenameLower, "normal", "nrm"))
                    layer.Normal = tex;
                else if (IsKeywordIn(filenameLower, "mask", "metallicsmoothness", "ms", "metal", "smooth"))
                    layer.Mask = tex;
            }

            Layers.Add(layer);
        }

        if (Layers.Count == 0)
            Debug.LogWarning("No valid texture layers found.");
        else
            Debug.Log($"✅ Successfully scanned {Layers.Count} layers.");
    }

    /// <summary>
    /// Helper to check if any keyword exists in the string.
    /// </summary>
    private bool IsKeywordIn(string filename, params string[] keywords)
    {
        foreach (string kw in keywords)
            if (filename.Contains(kw)) return true;
        return false;
    }

    [Title(" Layer List")]
    [ListDrawerSettings(
        NumberOfItemsPerPage = 8,
        DraggableItems = false,
        CustomAddFunction = nameof(AddNewLayer),
        ShowIndexLabels = false,
        HideAddButton = true,
        HideRemoveButton = true
    )]
    [TableList(ShowIndexLabels = false, IsReadOnly = false)]
    public List<TerrainLayerData> Layers = new List<TerrainLayerData>();

    private void AddNewLayer() { }

    [Button(" Export to Texture2DArrays")]
    [GUIColor(0.3f, 0.7f, 0.3f, 1.0f)]
    private void ExportToArrays()
    {
        if (Layers.Count == 0)
        {
            Debug.LogError("No layers to export! Please scan a folder first.");
            return;
        }

        // Validate first texture to get format/size
        Texture2D firstAlbedo = null;
        foreach (var layer in Layers)
        {
            if (layer.Albedo != null)
            {
                firstAlbedo = layer.Albedo;
                break;
            }
        }

        if (firstAlbedo == null)
        {
            Debug.LogError("No albedo textures found. Cannot determine texture size.");
            return;
        }

        int width = firstAlbedo.width;
        int height = firstAlbedo.height;
        TextureFormat format = firstAlbedo.format;

        // Optional: warn on mismatch
        foreach (var layer in Layers)
        {
            ValidateTexture(layer.Albedo, width, height, format, "Albedo");
            ValidateTexture(layer.Normal, width, height, format, "Normal");
            ValidateTexture(layer.Mask, width, height, format, "Mask");
        }

        // Create arrays
        Texture2DArray albedoArray = new Texture2DArray(width, height, Layers.Count, format, true);
        Texture2DArray normalArray = new Texture2DArray(width, height, Layers.Count, format, true);
        Texture2DArray maskArray = new Texture2DArray(width, height, Layers.Count, format, true);

        for (int i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (layer.Albedo) albedoArray.SetPixels32(layer.Albedo.GetPixels32(), i);
            if (layer.Normal) normalArray.SetPixels32(layer.Normal.GetPixels32(), i);
            if (layer.Mask) maskArray.SetPixels32(layer.Mask.GetPixels32(), i);
        }

        // Apply changes
        albedoArray.Apply();
        normalArray.Apply();
        maskArray.Apply();

        // Save assets
        string path = EditorUtility.SaveFilePanelInProject("Save Texture2DArrays", "TerrainArrays", "asset", 
            "Choose a name. Three assets will be created: _Albedo, _Normal, _Mask");
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(albedoArray, path + "_Albedo.asset");
        AssetDatabase.CreateAsset(normalArray, path + "_Normal.asset");
        AssetDatabase.CreateAsset(maskArray, path + "_Mask.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ Successfully created 3 Texture2DArrays with {Layers.Count} layers:\n{path}");
    }

    /// <summary>
    /// Validates texture size and logs warning if mismatched.
    /// </summary>
    private void ValidateTexture(Texture2D tex, int expectedW, int expectedH, TextureFormat expectedFormat, string label)
    {
        if (tex == null) return;
        if (tex.width != expectedW || tex.height != expectedH)
            Debug.LogWarning($"{label} '{tex.name}' size mismatch: {tex.width}x{tex.height} vs {expectedW}x{expectedH}");
        if (tex.format != expectedFormat)
            Debug.LogWarning($"{label} '{tex.name}' format mismatch: {tex.format} vs {expectedFormat}");
    }
}