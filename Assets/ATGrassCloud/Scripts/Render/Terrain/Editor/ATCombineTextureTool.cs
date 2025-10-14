using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using System.IO;
using System.Linq;
using Sirenix.OdinInspector.Editor;

namespace ATGrassCloud
{
    public class ATCombineTextureTool : OdinEditorWindow
    {
        [MenuItem("Tools/AT Grass Cloud/Texture Combiner (Batch to Subfolders)")]
        private static void OpenWindow()
        {
            GetWindow<ATCombineTextureTool>().Show();
        }

        [BoxGroup("Input Settings")]
        [FolderPath]
        public string inputRootFolder = "Assets/ATGrassCloud/Material/Terrain/SubMaterial";

        [BoxGroup("Naming Settings")]
        [ReadOnly]
        public string aoKeyword = "AO";

        [BoxGroup("Naming Settings")]
        public string roughnessKeyword = "Rough";

        [BoxGroup("Naming Settings")]
        public string metalKeyword = "Metal";

        [BoxGroup("Naming Settings")]
        [ReadOnly]
        public string emissionKeyword = "Emission";

        [BoxGroup("Output Settings")]
        public FilterMode filterMode = FilterMode.Bilinear;

        [BoxGroup("Output Settings")]
        public TextureWrapMode wrapMode = TextureWrapMode.Repeat;

        [Button("Select Input Folder", ButtonSizes.Medium)]
        private void SelectInputFolder()
        {
            string path = EditorUtility.OpenFolderPanel("Select Input Root Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                inputRootFolder = path;
            }
        }

        [Button("Start Batch Combine & Export", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 0.6f, 1f)]
        private void BatchCombineAndExport()
        {
            if (string.IsNullOrEmpty(inputRootFolder))
            {
                EditorUtility.DisplayDialog("Error", "Please specify the input folder.", "OK");
                return;
            }

            if (!Directory.Exists(inputRootFolder))
            {
                EditorUtility.DisplayDialog("Error", "Input folder does not exist.", "OK");
                return;
            }

            string[] subDirs = Directory.GetDirectories(inputRootFolder);

            if (subDirs.Length == 0)
            {
                Debug.LogWarning("No subfolders found in input directory.");
                return;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (string subDir in subDirs)
            {
                string subDirName = new DirectoryInfo(subDir).Name;
                string outputPath = Path.Combine(subDir, $"{subDirName}_Mask.png"); // Save directly in subfolder

                if (ProcessSubFolder(subDir, outputPath))
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }

            Debug.Log($"✅ Batch complete: {successCount} succeeded, {failCount} failed.");
            AssetDatabase.Refresh(); // Make new files appear in Unity Project window
        }

        /// <summary>
        /// Processes a single subfolder: finds AO, Roughness, Metal, Emission textures,
        /// combines them into a single RGBA texture, and saves it as _Mask.png in the same folder.
        /// </summary>
        /// <param name="folderPath">Path to the subfolder containing textures</param>
        /// <param name="outputPath">Full path where the combined texture will be saved</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool ProcessSubFolder(string folderPath, string outputPath)
        {
            // Search for common texture formats
            string[] pngFiles = Directory.GetFiles(folderPath, "*.png");
            string[] tgaFiles = Directory.GetFiles(folderPath, "*.tga");
            string[] jpgFiles = Directory.GetFiles(folderPath, "*.jpg");
            string[] tiffFiles = Directory.GetFiles(folderPath, "*.tiff");

            string[] files = pngFiles.Union(tgaFiles).Union(jpgFiles).Union(tiffFiles).ToArray();

            if (files.Length == 0)
            {
                Debug.LogWarning($"No image files found in: {folderPath}");
                return false;
            }

            // Find texture paths by keyword (case-insensitive)
            string aoPath = FindTexturePath(files, aoKeyword);
            string roughPath = FindTexturePath(files, roughnessKeyword);
            string metalPath = FindTexturePath(files, metalKeyword);
            string emissionPath = FindTexturePath(files, emissionKeyword);

            // if (string.IsNullOrEmpty(aoPath))
            // {
            //     Debug.LogError($"AO texture not found in: {folderPath}");
            //     return false;
            // }
            if (string.IsNullOrEmpty(roughPath))
            {
                Debug.LogError($"Roughness texture not found in: {folderPath}");
                return false;
            }
            if (string.IsNullOrEmpty(metalPath))
            {
                Debug.LogError($"Metallic texture not found in: {folderPath}");
                return false;
            }
            // if (string.IsNullOrEmpty(emissionPath))
            // {
            //     Debug.LogError($"Emission texture not found in: {folderPath}");
            //     return false;
            // }

            // Load textures from asset database
            // Texture2D aoTex = LoadTextureAsset(aoPath);
            Texture2D roughTex = LoadTextureAsset(roughPath);
            Texture2D metalTex = LoadTextureAsset(metalPath);
            // Texture2D emissionTex = LoadTextureAsset(emissionPath);

            if (roughTex == null || metalTex == null)
            {
                Debug.LogError("Failed to load one or more textures.");
                return false;
            }

            // Validate all textures have the same dimensions
            int w = roughTex.width, h = roughTex.height;
            if (roughTex.width != w || roughTex.height != h ||
                metalTex.width != w || metalTex.height != h)
            {
                Debug.LogError($"Texture size mismatch in folder: {folderPath}");
                return false;
            }

            // Create temporary RenderTexture in linear color space
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            rt.filterMode = filterMode;
            rt.wrapMode = wrapMode;
            rt.Create();

            // Set active render target (for ReadPixels and GL drawing)
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = rt;

            // Clear render target
            GL.Clear(false, true, Color.clear);

            // Use custom shader to combine channels
            Material combineMat = new Material(Shader.Find("Hidden/CombineRGBAChannels"));
            // combineMat.SetTexture("_AOMap", aoTex);
            combineMat.SetTexture("_RoughnessMap", roughTex);
            combineMat.SetTexture("_MetalMap", metalTex);
            // combineMat.SetTexture("_EmissionMap", emissionTex);

            // Draw full-screen quad
            GL.PushMatrix();
            GL.LoadOrtho();
            combineMat.SetPass(0);

            GL.Begin(GL.QUADS);
            GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
            GL.End();

            GL.PopMatrix();

            // Create final texture
            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false, true); // linear, not readable
            result.filterMode = filterMode;
            result.wrapMode = wrapMode;

            // Read pixels from active RenderTexture
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();

            // Restore previous active render texture
            RenderTexture.active = previousActive;

            // Cleanup
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(combineMat);

            // Encode and save as PNG
            byte[] pngData = result.EncodeToPNG();
            File.WriteAllBytes(outputPath, pngData);

            Debug.Log($"Saved combined mask: {outputPath}");

            // Destroy temporary texture
            Object.DestroyImmediate(result);
            return true;
        }

        /// <summary>
        /// Finds the first file path containing the given keyword in its filename (case-insensitive).
        /// </summary>
        /// <param name="files">Array of file paths</param>
        /// <param name="keyword">Keyword to search for (e.g., "AO", "Rough")</param>
        /// <returns>Matching file path, or null if not found</returns>
        private string FindTexturePath(string[] files, string keyword)
        {
            return files.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f)
                        .ToUpper()
                        .Contains(keyword.ToUpper()));
        }

        /// <summary>
        /// Loads a Texture2D asset from a file system path (must be inside Assets folder).
        /// </summary>
        /// <param name="fullPath">Absolute path on disk</param>
        /// <returns>Loaded Texture2D, or null if failed</returns>
        private Texture2D LoadTextureAsset(string fullPath)
        {
            string assetPath = fullPath.Replace(Application.dataPath + "/", "Assets/");
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogError($"Failed to load texture: {assetPath}");
            }
            return tex;
        }
    }

}