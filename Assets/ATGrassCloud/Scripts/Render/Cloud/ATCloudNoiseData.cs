using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace ATGrassCloud
{

    [System.Serializable]
    public class NoiseSettings
    {
        public enum NoiseChannel { R, G, B, A }
        public enum NoiseType { Worley, Perlin , Simplex, Curl }
        [BoxGroup("Base")]
        [OnValueChanged("OnUpdateSettings")]
        [ReadOnly]
        public int texSize;
        [BoxGroup("Base")]
        [Title("Channel - R/G/B/A")]
        [OnValueChanged("OnUpdateSettings")]
        [ReadOnly]
        public NoiseChannel channelName;
        [BoxGroup("Base")]
        [OnValueChanged("OnUpdateSettings")]
        [OnValueChanged("OnUpdateChannel")]
        [ReadOnly]
        public int channelID;
        [BoxGroup("Parameters")]
        [OnValueChanged("OnUpdateSettings")]
        public int seed;
        [BoxGroup("Parameters")]
        [OnValueChanged("OnUpdateSettings")]
        [Range(0,1.0f)]
        public float mix;
        [BoxGroup("Parameters")]
        [OnValueChanged("OnUpdateSettings")]
        public NoiseType noiseType;
        [BoxGroup("Parameters")]
        [OnValueChanged("OnUpdateSettings")]
        [Range(1,64)]
        public int frequencyA;
        [BoxGroup("Parameters")]
        [OnValueChanged("OnUpdateSettings")]
        [Range(1,64)]
        public int frequencyB;
        [BoxGroup("Parameters")]
        [OnValueChanged("OnUpdateSettings")]
        [Range(1,64)]
        public int frequencyC;
        [BoxGroup("Display")]
        [OnValueChanged("OnUpdateSettings")]
        [Range(0, 1.0f)]
        public float zIndexRange;

        [BoxGroup("Display")]
        [PreviewField(128)]
        [ReadOnly]
        public Texture2D noiseDisplay;

        [HideInInspector]
        [ReadOnly]
        public  ATCloudNoiseData cloudNoiseData;

        public NoiseSettings Clone()
        {
            return JsonUtility.FromJson<NoiseSettings>(JsonUtility.ToJson(this));
        }
        
        public void OnUpdateChannel()
        {
            channelName = (NoiseChannel)channelID;
        }
        public void Set(NoiseSettings settings)
        {
            texSize = settings.texSize;
            channelName = settings.channelName;
            channelID = settings.channelID;
            seed = settings.seed;
            mix = settings.mix;
            frequencyA = settings.frequencyA;
            frequencyB = settings.frequencyB;
            frequencyC = settings.frequencyC;
            zIndexRange = settings.zIndexRange;
        }

        public void OnUpdateSettings()
        {
            if (cloudNoiseData == null)
            {
                return;
            }
            cloudNoiseData.UpdateSettings(this);
        }
    }

    [CreateAssetMenu(fileName = "ATCloudNoiseData", menuName = "ATGrassCloud/Cloud Noise Data" , order = 100)]
    public class ATCloudNoiseData : ScriptableObject
    {
        [TabGroup("LowFrequency")]
        public NoiseSettings lowNoiseSettings;
        [TabGroup("MidFrequency")]
        public NoiseSettings midNoiseSettings;
        [TabGroup("HighFrequency")]
        public NoiseSettings highNoiseSettings;
        [TabGroup("HighestFrequency")]
        public NoiseSettings highestNoiseSettings;

        [BoxGroup("Settings")]
        public ComputeShader noiseComputeShader;

        private static readonly int[] OptionalTexSizeValues = { 32, 64, 128, 256, 512 };

        [ValueDropdown("OptionalTexSizeValues")]
        [BoxGroup("Settings")]
        [OnValueChanged("UpdateTexSize")]
        public int texSize = 64;

        [BoxGroup("Output")]
        [ShowInInspector]
        private RenderTexture noiseTexture;

        [BoxGroup("Output")]
        [ShowInInspector]
        private Texture3D noiseTextureExported;

        public Texture NoiseTex
        {
            get { if ( noiseTextureExported != null) return noiseTextureExported; return noiseTexture; }
        }

        private bool isInited = false;
        private List<ComputeBuffer> buffers;

        public static readonly int KERNEL_NOISE_ID = 0;
        public static readonly int KERNEL_NORMALIZE_ID = 1;
        public static readonly int KERNEL_CROSS_SECTION_ID = 2;

        public ATCloudNoiseData()
        {
        }

        public bool ShouldBeInited()
        {
            return noiseTexture == null || noiseTexture.width != texSize || !isInited;
        }

        public void UpdateSettings( NoiseSettings settings , bool updateDisplay = true )
        {
            UpdateNoise(settings);
            if (updateDisplay)
            {
                settings.noiseDisplay = GetCrossSectionByChannel(settings.channelID, (int)(settings.zIndexRange * (texSize - 1)));
            }
        }


        public void UpdateNoise(NoiseSettings settings )
        {
            if ( settings == null || !isInited )
                return;

            {
                RenderTexture texture = noiseTexture;
                buffers = new List<ComputeBuffer>();

                noiseComputeShader.SetFloat("layerMix", settings.mix);
                noiseComputeShader.SetInt("resolution", texSize);
                noiseComputeShader.SetVector("channelMask", ChannelMask(settings.channelID));
                noiseComputeShader.SetTexture(KERNEL_NOISE_ID, "result", texture);
                var limitsBuffer = SetBuffer(new int[] { int.MaxValue, 0 }, sizeof(int), "limits");
                noiseComputeShader.SetInt("noiseType", (int)settings.noiseType);
                UpdateProperties(settings);
    
                int threads = Mathf.CeilToInt(texSize / 8.0f);
                noiseComputeShader.Dispatch(KERNEL_NOISE_ID, threads, threads, threads);
            
                noiseComputeShader.SetBuffer(KERNEL_NORMALIZE_ID, "limits", limitsBuffer);
                noiseComputeShader.SetTexture(KERNEL_NORMALIZE_ID, "result", texture);
                noiseComputeShader.Dispatch(KERNEL_NORMALIZE_ID, threads, threads, threads);

                foreach (var buffer in buffers)
                    buffer.Release();
            }
        }

        void GenerateRandomPoints(System.Random rand, int numCells, string buffer)
        {
            Vector3[] points = new Vector3[(int)System.Math.Pow(numCells, 3)];
            
            for (int x = 0; x < numCells; x++)
            {
                for (int y = 0; y < numCells; y++)
                {
                    for (int z = 0; z < numCells; z++)
                    {
                        Vector3 randomPosition = new Vector3(
                            (float)rand.NextDouble(),
                            (float)rand.NextDouble(),
                            (float)rand.NextDouble());
                        int index = x + numCells * (y + z * numCells);
                        points[index] = (new Vector3(x, y, z) + randomPosition) / (float)numCells;
                    }
                }
            }

            SetBuffer(points, sizeof(float) * 3, buffer);
        }
        void UpdateProperties(NoiseSettings settings)
        {
            System.Random rand = new System.Random(settings.seed);
            GenerateRandomPoints(rand, settings.frequencyA, "pointsA");
            GenerateRandomPoints(rand, settings.frequencyB, "pointsB");
            GenerateRandomPoints(rand, settings.frequencyC, "pointsC");

            noiseComputeShader.SetInt("frequencyA", settings.frequencyA);
            noiseComputeShader.SetInt("frequencyB", settings.frequencyB);
            noiseComputeShader.SetInt("frequencyC", settings.frequencyC);
        }
        
        public Vector4 ChannelMask(int index)
        {
            Vector4 channelWeight = new Vector4();
            channelWeight[(int)index] = 1;
            return channelWeight;
        }

        ComputeBuffer SetBuffer(System.Array data, int stride, string bufferName)
        {
            var buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Structured);
            buffer.SetData(data);
            buffers.Add(buffer);
            noiseComputeShader.SetBuffer(KERNEL_NOISE_ID, bufferName, buffer);
            return buffer;
        }

        public void UpdateTexSize()
        {
            lowNoiseSettings.texSize = texSize;
            midNoiseSettings.texSize = texSize;
            highNoiseSettings.texSize = texSize;
            highestNoiseSettings.texSize = texSize;
        }

        public void UpdateChannel()
        {
            lowNoiseSettings.channelID = 0;
            lowNoiseSettings.channelName = NoiseSettings.NoiseChannel.R;
            midNoiseSettings.channelID = 1;
            midNoiseSettings.channelName = NoiseSettings.NoiseChannel.G;
            highNoiseSettings.channelID = 2;
            highNoiseSettings.channelName = NoiseSettings.NoiseChannel.B;
            highestNoiseSettings.channelID = 3;
            highestNoiseSettings.channelName = NoiseSettings.NoiseChannel.A;
        }


        public void UpdateParent()
        {
            lowNoiseSettings.cloudNoiseData = this;
            midNoiseSettings.cloudNoiseData = this;
            highNoiseSettings.cloudNoiseData = this;
            highestNoiseSettings.cloudNoiseData = this;
        }

        public void InitTexture()
        {
            noiseTexture = CreateTexture(texSize);
        }

        [Button("Reset")]
        public void SetToDefault()
        {
            isInited = false;

            texSize = 128;
            InitTexture();
            UpdateParent();
            UpdateTexSize();
            UpdateChannel();
            UpdateSeed();

            lowNoiseSettings.mix = Random.Range(0.0f, 1.0f);
            lowNoiseSettings.frequencyA = Random.Range(3, 4);
            lowNoiseSettings.frequencyB = Random.Range(5, 8);
            lowNoiseSettings.frequencyC = Random.Range(9, 11);

            midNoiseSettings.mix = Random.Range(0.0f, 1.0f);
            midNoiseSettings.frequencyA = Random.Range(9, 11);
            midNoiseSettings.frequencyB = Random.Range(13, 18);
            midNoiseSettings.frequencyC = Random.Range(19, 22);

            highNoiseSettings.mix = Random.Range(0.0f, 1.0f);
            highNoiseSettings.frequencyA = Random.Range(23, 28);
            highNoiseSettings.frequencyB = Random.Range(28, 32);
            highNoiseSettings.frequencyC = Random.Range(32, 37);

            highestNoiseSettings.mix = Random.Range(0.0f, 1.0f);
            highestNoiseSettings.frequencyA = Random.Range(47, 52);
            highestNoiseSettings.frequencyB = Random.Range(52, 57);
            highestNoiseSettings.frequencyC = Random.Range(57, 62);

            isInited = true;
            UpdateSettings(lowNoiseSettings);
            UpdateSettings(midNoiseSettings);
            UpdateSettings(highNoiseSettings);
            UpdateSettings(highestNoiseSettings);

        }

        
        [Button("UpdateSeed")]
        public void UpdateSeed()
        {
            lowNoiseSettings.seed = Random.Range(0, 100000);
            midNoiseSettings.seed = Random.Range(0, 100000);
            highNoiseSettings.seed = Random.Range(0, 100000);
            highestNoiseSettings.seed = Random.Range(0, 100000);
        }

        [Button("Generate Noise By Parameters", ButtonSizes.Large)]
        
        public void GenerateNoise()
        {
            isInited = false;
            InitTexture();
            UpdateParent();
            UpdateTexSize();
            UpdateChannel();
            isInited = true;

            UpdateSettings(lowNoiseSettings);
            UpdateSettings(midNoiseSettings);
            UpdateSettings(highNoiseSettings);
            UpdateSettings(highestNoiseSettings);

            Debug.Log("Finish Generate Noise: size " + texSize );
        }

        [Button("ExportNoise")]
        public void ExportNoise()
        {
            GenerateNoise();

            Texture3D texture3D = CreateTexture3DFrom3DRenderTexture(noiseTexture);


            if (texture3D == null)
            {
                Debug.LogError("Failed to create Texture3D from the source.");
                return;
            }
            
            // apply filter setting 
            texture3D.filterMode = FilterMode.Trilinear;
            texture3D.wrapMode = TextureWrapMode.Repeat;
#if UNITY_EDITOR
            // create directory if not exist
            // get path of data 
            string exportPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            exportPath = exportPath.Replace(name, $"{name}_Output");

            Debug.Log("Export Path is " + exportPath);

            // 保存为 Asset
            UnityEditor.AssetDatabase.CreateAsset(texture3D, exportPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
#endif 
            noiseTextureExported = texture3D;

        }

        /// <summary>
        /// Creates a CPU-readable Texture3D from a 3D RenderTexture by copying its data layer by layer.
        /// This is useful when you need to access texture data on the CPU (e.g., for saving, analysis, or post-processing).
        /// </summary>
        /// <param name="rt">The source 3D RenderTexture to convert.</param>
        /// <returns>A new Texture3D with the same dimensions and data as the input RenderTexture, or null if invalid.</returns>
        private Texture3D CreateTexture3DFrom3DRenderTexture(RenderTexture rt)
        {
            // Validate that the input RenderTexture is actually a 3D texture
            if (rt.dimension != UnityEngine.Rendering.TextureDimension.Tex3D)
            {
                Debug.LogError("RenderTexture must be 3D.");
                return null;
            }

            // Extract texture dimensions and format from the source RenderTexture
            int width = rt.width;
            int height = rt.height;
            int depth = rt.volumeDepth;
            GraphicsFormat format = rt.graphicsFormat;

            // Create a temporary 3D RenderTexture to safely copy and read from
            RenderTexture tempRT = new RenderTexture(rt.width, rt.height, rt.depth, rt.graphicsFormat);
            tempRT.volumeDepth = rt.volumeDepth; // Ensure depth is properly set for 3D textures
            Debug.Log("Volume depth: " + tempRT.volumeDepth);
            tempRT.enableRandomWrite = true; // Enable GPU random write access if needed
            tempRT.dimension = UnityEngine.Rendering.TextureDimension.Tex3D; // Explicitly set dimension
            tempRT.filterMode = FilterMode.Point; // Use point filtering to avoid interpolation
            tempRT.Create(); // Allocate GPU memory

            // Copy the contents of the original RenderTexture to the temporary one
            Graphics.CopyTexture(rt, tempRT);

            // Create a CPU-accessible Texture3D with the same properties
            Texture3D texture3D = new Texture3D(width, height, depth, format, TextureCreationFlags.None);

            // Array to hold all pixel data from the 3D texture (flattened into 1D)
            Color[] allPixels = new Color[width * height * depth];

            // Store currently active RenderTexture so we can restore it later
            RenderTexture activeRT = RenderTexture.active;

            // Set the temporary 3D texture as active for reading
            RenderTexture.active = tempRT;

            // Loop through each depth slice (z-layer) of the 3D texture
            for (int z = 0; z < depth; z++)
            {
                // Get the 2D texture representing the cross-section at depth z
                var texture = GetCrossSectionTexture(z);
                
                if (texture != null)
                {
                    // Read all pixels from the current layer
                    var pixels = texture.GetPixels();
                    int depthOffset = z * width * height; // Calculate starting index for this layer

                    // Copy pixels from current layer into the correct position in the full 3D pixel array
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        allPixels[depthOffset + i] = pixels[i];
                    }
                }
            }

            // Restore previously active RenderTexture
            RenderTexture.active = activeRT;

            // Apply the pixel data to the Texture3D
            texture3D.SetPixels(allPixels);
            texture3D.Apply(); // Upload data to GPU

            // Clean up temporary GPU resources
            tempRT.Release();
            // Note: temp2DRT is created but not used in this code path — consider releasing it too
            // if it's used elsewhere or remove it if unnecessary.

            return texture3D;
        }


        RenderTexture CreateTexture(int size)
        {
            RenderTexture output = new RenderTexture(size, size, 0);
            output.wrapMode = TextureWrapMode.Repeat;
            output.filterMode = FilterMode.Bilinear;
            output.volumeDepth = size;
            output.enableRandomWrite = true;
            output.dimension = TextureDimension.Tex3D;
            output.graphicsFormat = GraphicsFormat.R16G16B16A16_UNorm;
            output.name = "CloudNoise3D";
            output.Create();

            
            return output;
        }

        public RenderTexture GetCrossSectionRT( int zIndex)
        {
            RenderTexture _noiseTexture = noiseTexture;
            int size = _noiseTexture.width;
        
            noiseComputeShader.SetTexture(KERNEL_CROSS_SECTION_ID, "noiseTexture", _noiseTexture);
            RenderTexture crossSection = new RenderTexture(size, size, 0);

            crossSection.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            crossSection.enableRandomWrite = true;
            crossSection.filterMode = FilterMode.Point;
            crossSection.Create();

            noiseComputeShader.SetTexture(KERNEL_CROSS_SECTION_ID, "crossSection", crossSection);
            noiseComputeShader.SetInt("zIndex", zIndex);
            int numThreadGroups = Mathf.CeilToInt(size / 32f);
            noiseComputeShader.Dispatch(KERNEL_CROSS_SECTION_ID, numThreadGroups, numThreadGroups, 1);
            return crossSection;
        }

        public Texture2D GetCrossSectionTexture( int zIndex) {
            var crossSection = GetCrossSectionRT(zIndex);
            return ToTexture2D(crossSection);
        }


        public Texture2D GetCrossSectionByChannel( int channel, int zIndex) {
            var crossSection = GetCrossSectionRT(zIndex);
            return GetChannelTexture(ToTexture2D(crossSection), channel);
        }

        public Texture2D GetChannelTexture(Texture2D inputTexture, int index) {
            if (inputTexture == null) {
                return null;
            }
            int size = inputTexture.width;
            Texture2D output = new Texture2D(size, size);

            Color[] pixels = inputTexture.GetPixels();
            Color[] channel = new Color[pixels.Length];
            for (int j = 0; j < pixels.Length; j++)
            {
                float val = pixels[j][index];
                channel[j] = new Color(val, val, val);
            }
            output.SetPixels(channel);
            output.Apply();
            
            return output;
        }
        Texture2D ToTexture2D(RenderTexture rendered)
        {
            Texture2D output = new Texture2D(rendered.width, rendered.height);
            RenderTexture.active = rendered;
            output.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
            output.Apply();
            RenderTexture.active = null;
            return output;
        }

    }

}