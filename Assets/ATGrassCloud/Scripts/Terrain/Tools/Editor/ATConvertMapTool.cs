using System.IO;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.Security.Authentication.ExtendedProtection;

#if UNITY_EDITOR
public class ATConvertMapTool : OdinEditorWindow
{
    [MenuItem("Tools/AT Grass Cloud/EXR R Channel Extraction Tool", priority = 1000)]
    private static void OpenWindow()
    {
        GetWindow<ATConvertMapTool>("AT Convert Map Tool");
    }

    [Title("RGBA Channel Extraction Tool", "Extract the R channel of an EXR file and output it as a monochrome HDR-EXR")]
    [BoxGroup("Input")]
    [HideLabel, HideReferenceObjectPicker]
    public InputSettings input = new InputSettings();

    [BoxGroup("Output")]
    [HideLabel, HideReferenceObjectPicker]
    public OutputSettings output = new OutputSettings();


    public static Color EncodeFloatRGBA16( float v)
    {
        Vector4 kEncodeMul = new Vector4(1.0f, 15.0f, 225.0f, 3375.0f);
        float kEncodeBit = 1.0f / 15.0f;
        Vector4 enc = kEncodeMul * v;
        enc.x = enc.x - Mathf.Floor(enc.x);
        enc.y = enc.y - Mathf.Floor(enc.y);
        enc.z = enc.z - Mathf.Floor(enc.z);
        enc.w = enc.w - Mathf.Floor(enc.w);
        enc -= new Vector4(enc.y, enc.z, enc.w, enc.w) * kEncodeBit;
        return new Color(enc.x, enc.y, enc.z, enc.w);
    }


    public static float DecodeFloatRGBA16(Color c)
    {
        Vector4 kDecodeMul = new Vector4(1.0f, 1.0f / 15.0f, 1.0f /  225.0f, 1.0f / 3375.0f);
        return Vector4.Dot(c, kDecodeMul);
    }

    [Button("🔄 Extract and Save", ButtonSizes.Large, Style = ButtonStyle.Box)]
    [GUIColor(0.4f, 0.8f, 1.0f)]
    private void ExtractAndSaveRChannel()
    {
        if (input.sourceTexture == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select an EXR file first!", "OK");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(input.sourceTexture);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.ToLower().EndsWith(".exr"))
        {
            EditorUtility.DisplayDialog("Error", "Please select a valid .exr file!", "OK");
            return;
        }

        // 验证 Read/Write Enabled
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            var texSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(texSettings);
            if (!texSettings.readable)
            {
                EditorUtility.DisplayDialog("Error", $"Texture '{input.sourceTexture.name}' must have 'Read/Write Enabled' enabled to read data.", "OK");
                return;
            }
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        int width = tex.width;
        int height = tex.height;

        float heightMin = float.MaxValue;
        float heightMax = float.MinValue;

        // 获取像素
        Color[] pixels = tex.GetPixels();
        Texture2D outputTex = new Texture2D(width, height, TextureFormat.RGBA4444, true);
        Color[] outputPixels = new Color[pixels.Length];

        float multiplier = input.rMultiplier;

        for (int i = 0; i < pixels.Length; i++)
        {
            float r = pixels[i].r;
            heightMin = Mathf.Min(heightMin, r);
            heightMax = Mathf.Max(heightMax, r);
            outputPixels[i] = EncodeFloatRGBA16(r );
        }

        Debug.Log($"Height Min: {heightMin}, Height Max: {heightMax}");

        outputTex.SetPixels(outputPixels);
        outputTex.Apply();

        //byte[] exrData = outputTex.EncodeToEXR(Texture2D.EXRFlags.None);
        byte[] exrData;
        switch (output.exportType)
        {
            case OutputSettings.ExportType.EXR:
                exrData = outputTex.EncodeToEXR(Texture2D.EXRFlags.None);
                break;
            case OutputSettings.ExportType.PNG:
                exrData = outputTex.EncodeToPNG();
                break;
            case OutputSettings.ExportType.TGA:
                exrData = outputTex.EncodeToTGA();
                break;
            case OutputSettings.ExportType.JPG:
                exrData = outputTex.EncodeToJPG();
                break;
            default:
                exrData = null;
                break;
        }

        Object.DestroyImmediate(outputTex);

        if (exrData == null)
        {
            Debug.LogError("EXR encoding failed!");
            return;
        }

        string fullPath = Path.GetFullPath(output.savePath);
        string dir = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(fullPath) && !output.overwriteWithoutPrompt)
        {
            if (!EditorUtility.DisplayDialog("File Exists", $"File {output.savePath} already exists. Overwrite?", "Overwrite", "Cancel"))
                return;
        }

        string outputPath = $"{output.savePath}/{Path.GetFileNameWithoutExtension(input.sourceTexture.name)}_R.{output.exportType.ToString().ToLower()}";
        File.WriteAllBytes(outputPath, exrData);
        AssetDatabase.Refresh();


        Debug.Log($"✅ R channel saved to:\n{outputPath}");
        ShowNotification(new GUIContent("✔ Save successful!"));
    }



    // 拖拽支持
    protected override void OnEnable()
    {
        base.OnEnable();
        wantsMouseMove = true;
    }


    // protected override void OnGUI()
    // {

    //     Event e = Event.current;
    //     Rect dropArea = GUILayoutUtility.GetLastRect();

    //     switch (e.type)
    //     {
    //         case EventType.DragUpdated:
    //         case EventType.DragPerform:
    //             if (!dropArea.Contains(e.mousePosition)) break;

    //             DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

    //             if (e.type == EventType.DragPerform)
    //             {
    //                 DragAndDrop.AcceptDrag();
    //                 foreach (Object dragged in DragAndDrop.objectReferences)
    //                 {
    //                     if (dragged is Texture2D tex && AssetDatabase.GetAssetPath(tex).ToLower().EndsWith(".exr"))
    //                     {
    //                         input.sourceTexture = tex;
    //                         // 自动设置输出路径
    //                         string inputPath = AssetDatabase.GetAssetPath(tex);
    //                         string fileName = Path.GetFileNameWithoutExtension(inputPath) + "_R.exr";
    //                         output.savePath = Path.Combine(Path.GetDirectoryName(inputPath), fileName).Replace("\\", "/");
    //                         break;
    //                     }
    //                 }
    //                 GUI.changed = true;
    //             }
    //             break;
    //     }

    //     // 正常绘制UI
    //     GUILayout.Space(10);
    //     EditorGUILayout.HelpBox("💡 提示：你可以将 .exr 文件直接拖拽到此窗口中。", MessageType.Info);
    //     GUILayout.Space(10);
    // }
}

// 输入设置类
public class InputSettings
{
    [LabelText("Source EXR Texture")]
    [ValidateInput("IsEXRTexture", "Must be an .exr file")]
    public Texture2D sourceTexture;

    [LabelText("R Channel Intensity")]
    [Range(0.1f, 5.0f)]
    [InfoBox("Enhance or weaken the R channel output value")]
    public float rMultiplier = 1.0f;

    private bool IsEXRTexture(Texture2D tex)
    {
        if (tex == null) return true;
        string path = AssetDatabase.GetAssetPath(tex);
        return string.IsNullOrEmpty(path) || path.ToLower().EndsWith(".exr");
    }
}

// 输出设置类
public class OutputSettings
{
    public enum  ExportType { EXR, PNG , TGA  , JPG }

    [LabelText("Export Type")]
    public ExportType exportType = ExportType.EXR;

    [FolderPath( ParentFolder = "")]
    [SuffixLabel(".exr", overlay: true)]
    [LabelText("Output Path")]
    public string savePath = "Assets/ATGrassCloud/Data/Terrain/Convert";

    [ToggleLeft]
    [LabelText("Silent Overwrite")]
    public bool overwriteWithoutPrompt = false;
}

#endif // UNITY_EDITOR