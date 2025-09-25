
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector.Editor;
using Sirenix.OdinInspector;
using System;
using System.IO;

/// <summary>
/// 使用 Odin Inspector 创建的 Cubemap 天地光烘焙工具。
/// 该工具使用一个临时球体和指定的 Shader 来生成新的 Cubemap。
/// </summary>
public class CubemapCreator : OdinEditorWindow
{

    // ========== 内部资源 ==========
    [BoxGroup("引用资产")]
    [ShowInInspector]
    public Material bakeMaterial;
    // ========== Odin 窗口设置 ==========
    [MenuItem("Tools/AT Grass Cloud/Cubemap Creator")]
    private static void OpenWindow()
    {
        var window = GetWindow<CubemapCreator>();
        window.titleContent = new GUIContent("Cubemap Creator");
        window.Show();

    }

    [BoxGroup("输出设置"), LabelText("导出资产名称")]
    public string assetName = "BakedCubemap";

    [BoxGroup("输出设置"), LabelText("立方体贴图分辨率")]
    public int cubemapSize = 128;

    [BoxGroup("输出设置"), LabelText("是否包含 Mip Maps")]
    public bool generateMips = false;

    [BoxGroup("输出设置"), LabelText("输出路径")]
    public string outputPath = "Assets/ATGrassCloud/Data/GI/Cubemaps/";


    

    // ========== 初始化 ==========
    protected override void OnEnable()
    {
        base.OnEnable();
    }

    // ========== 主要 UI ==========
    [BoxGroup("操作")]
    // [Button("烘焙Tex2D 并导出 Cubemap", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 0.4f)]
    private void BakeAndExportCubemap()
    {
        if (bakeMaterial == null)
        {
            Debug.LogError("烘焙资源未初始化。");
            return;
        };

        // 解析最终分辨率
        int finalSize = cubemapSize;
        // 确保是 2 的幂（可选，但推荐）
        finalSize = Mathf.ClosestPowerOfTwo(finalSize +1 );
        finalSize = Mathf.Clamp(finalSize, 16, 2048); // 合理范围

        try
        {
            // 创建RenderTexture
            RenderTexture renderTexture = new RenderTexture(finalSize, finalSize, 24);
            renderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D; // 默认是2D纹理
                        
            // 使用bakeMaterial进行渲染
            Graphics.SetRenderTarget(renderTexture);
            GL.Clear(true, true, Color.clear);
            Graphics.Blit(null, renderTexture, bakeMaterial, 0);

            // 从RenderTexture读取数据到Texture2D
            Texture2D texture2D = new Texture2D(finalSize, finalSize, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, finalSize, finalSize), 0, 0);
            texture2D.Apply();

            // 将Texture2D保存为PNG文件
            byte[] bytes = texture2D.EncodeToPNG();
            string assetPath = $"{outputPath}{assetName}.png";
            System.IO.File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath);

            // 修改导入设置，将其作为Cubemap处理
            TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.textureShape = TextureImporterShape.Texture2D; // 使用2D纹理
                textureImporter.isReadable = true; // 确保可以读取
                textureImporter.SaveAndReimport(); // 保存并重新导入以应用更改
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("错误", $"烘焙失败: {e.Message}", "确定");
        }
    }


     [BoxGroup("操作")]
    [Button("烘焙Skybox 并导出 Cubemap", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 0.4f)]
    private void BakeAndExportSkyboxCubemap()
    {
        if (bakeMaterial == null)
        {
            Debug.LogError("烘焙资源未初始化。");
            return;
        };

        // 解析最终分辨率
        int finalSize = cubemapSize;
        // 确保是 2 的幂（可选，但推荐）
        finalSize = Mathf.ClosestPowerOfTwo(finalSize);
        finalSize = Mathf.Clamp(finalSize, 16, 2048); // 合理范围

        try
        {
            // 创建RenderTexture
            RenderTexture renderTexture = new RenderTexture(finalSize, finalSize, 0);
            renderTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D; // 默认是2D纹理
            renderTexture.wrapMode = TextureWrapMode.Clamp;
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.Create();

            
            // 创建临时摄像机
            GameObject go = new GameObject("BakeCamera");
            Camera camera = go.AddComponent<Camera>();
            camera.fieldOfView = 90;
            camera.aspect = (float)finalSize / finalSize;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.cullingMask = 0; // 只渲染天空盒
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = Color.black;
            camera.renderingPath = RenderingPath.Forward;
            camera.targetTexture = renderTexture;

            // 设置天空盒材质
            RenderSettings.skybox = bakeMaterial;

            // 强制刷新内置变量
            camera.Render();

            // 从RenderTexture读取数据到Texture2D
            Texture2D texture2D = new Texture2D(finalSize, finalSize, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, finalSize, finalSize), 0, 0);
            texture2D.Apply();

            // 将Texture2D保存为PNG文件
            byte[] bytes = texture2D.EncodeToPNG();
            string assetPath = $"{outputPath}{assetName}_Sky.png";
            System.IO.File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath);

            // 修改导入设置，将其作为Cubemap处理
            TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (textureImporter != null)
            {
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.textureShape = TextureImporterShape.Texture2D; // 设置为2D纹理
                textureImporter.isReadable = true; // 确保可以读取s
                textureImporter.SaveAndReimport(); // 保存并重新导入以应用更改
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 高亮导出结果
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture>(assetPath));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("错误", $"烘焙失败: {e.Message}", "确定");
        }
    }


    [BoxGroup("调试"), FoldoutGroup("调试/信息", expanded: false)]
    private void DebugInfo()
    {
        GUILayout.Label($"Material: {(bakeMaterial != null ? bakeMaterial.name : "未创建")}", EditorStyles.miniLabel);
        GUILayout.Label($"Unity 版本: {Application.unityVersion}", EditorStyles.miniLabel);
        GUILayout.Label($"平台: {Application.platform}", EditorStyles.miniLabel);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 销毁动态创建的材质
        if (bakeMaterial != null)
        {
            DestroyImmediate(bakeMaterial);
            bakeMaterial = null;
        }
    }
}

#endif