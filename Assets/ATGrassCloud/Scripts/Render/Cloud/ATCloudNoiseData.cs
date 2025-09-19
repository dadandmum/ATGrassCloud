using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using System.IO;

namespace ATGrassCloud
{
    [System.Serializable]
    public class NoiseSettings
    {
        public enum NoiseChannel { R, G, B, A }
        // [OnValueChanged("OnUpdateSettings")]
        // public int type;
        [OnValueChanged("OnUpdateSettings")]
        [ReadOnly]
        public int texSize;
        [OnValueChanged("OnUpdateSettings")]
        [ReadOnly]
        public NoiseChannel channelDisplay;
        [OnValueChanged("OnUpdateSettings")]
        [OnValueChanged("OnUpdateChannel")]
        [ReadOnly]
        public int channel;
        [OnValueChanged("OnUpdateSettings")]
        public int seed;
        [OnValueChanged("OnUpdateSettings")]
        [Range(0,1.0f)]
        public float mix;
        [OnValueChanged("OnUpdateSettings")]
        [Range(1,64)]
        public int frequencyA;
        [OnValueChanged("OnUpdateSettings")]
        [Range(1,64)]
        public int frequencyB;
        [OnValueChanged("OnUpdateSettings")]
        [Range(1,64)]
        public int frequencyC;
        [OnValueChanged("OnUpdateSettings")]
        [Range(0, 1.0f)]
        public float zIndexRange;

        [PreviewField(128)]
        [ReadOnly]
        public Texture2D noiseDisplay;

        [ReadOnly]
        public  ATCloudNoiseData cloudNoiseData;

        public NoiseSettings Clone()
        {
            return JsonUtility.FromJson<NoiseSettings>(JsonUtility.ToJson(this));
        }
        
        public void OnUpdateChannel()
        {
            channelDisplay = (NoiseChannel)channel;
        }
        public void Set(NoiseSettings settings)
        {
            texSize = settings.texSize;
            channelDisplay = settings.channelDisplay;
            channel = settings.channel;
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

        [ShowInInspector]
        [ReadOnly]
        private RenderTexture noiseTexture;

        static public string assetDataPath = "ATGrassCloud/Data";

        [ShowInInspector]
        [ReadOnly]
        Texture3D noiseTextureExported;

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


        public void UpdateSettings( NoiseSettings settings , bool updateDisplay = true )
        {
            UpdateNoise(settings);
            if (updateDisplay)
            {
                settings.noiseDisplay = GetCrossSection(settings.channel, (int)(settings.zIndexRange * (texSize - 1)));
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
                noiseComputeShader.SetVector("channelMask", ChannelMask(settings.channel));
                noiseComputeShader.SetTexture(KERNEL_NOISE_ID, "result", texture);
                var limitsBuffer = SetBuffer(new int[] { int.MaxValue, 0 }, sizeof(int), "limits");
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
            lowNoiseSettings.channel = 0;
            lowNoiseSettings.channelDisplay = NoiseSettings.NoiseChannel.R;
            midNoiseSettings.channel = 1;
            midNoiseSettings.channelDisplay = NoiseSettings.NoiseChannel.G;
            highNoiseSettings.channel = 2;
            highNoiseSettings.channelDisplay = NoiseSettings.NoiseChannel.B;
            highestNoiseSettings.channel = 3;
            highestNoiseSettings.channelDisplay = NoiseSettings.NoiseChannel.A;
        }

        [Button("UpdateSeed")]
        public void UpdateSeed()
        {
            lowNoiseSettings.seed = Random.Range(0, 100000);
            midNoiseSettings.seed = Random.Range(0, 100000);
            highNoiseSettings.seed = Random.Range(0, 100000);
            highestNoiseSettings.seed = Random.Range(0, 100000);
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

            texSize = 64;
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

        [Button("UpdateNoise")]
        public void UpdateNoise()
        {
            InitTexture();

            UpdateSettings(lowNoiseSettings, false);
            UpdateSettings(midNoiseSettings, false);
            UpdateSettings(highNoiseSettings, false);
            UpdateSettings(highestNoiseSettings, false);
        }

        [Button("ExportNoise")]
        public void ExportNoise()
        {
            UpdateNoise();

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
            exportPath = exportPath.Replace(name, $"{name}_Exp");

            Debug.Log("Export Path is " + exportPath);

            // 保存为 Asset
            UnityEditor.AssetDatabase.CreateAsset(texture3D, exportPath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
#endif 

        }


        private Texture3D CreateTexture3DFrom3DRenderTexture(RenderTexture rt)
        {
            if (rt.dimension != UnityEngine.Rendering.TextureDimension.Tex3D)
            {
                Debug.LogError("RenderTexture must be 3D.");
                return null;
            }

            // 创建 RenderTexture 的副本以读取数据
            RenderTexture tempRT = new RenderTexture(rt.width, rt.height, rt.depth, rt.graphicsFormat);
            tempRT.volumeDepth = rt.volumeDepth;
            Debug.Log("Volume " + tempRT.volumeDepth);
            tempRT.enableRandomWrite = true;
            tempRT.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            tempRT.enableRandomWrite = true; // 可能需要，取决于原始 RT
            
            tempRT.Create();

            // 复制内容
            Graphics.CopyTexture(rt, tempRT);

            // 创建 CPU 可读的 Texture3D
            Texture3D texture3D = new Texture3D(rt.width, rt.height, rt.volumeDepth, rt.graphicsFormat, TextureCreationFlags.None);

            // 逐层读取数据
            RenderTexture.active = tempRT;
            for (int z = 0; z < rt.volumeDepth; z++)
            {
                // 创建临时 2D RenderTexture 来读取当前切片
                RenderTexture sliceRT = RenderTexture.GetTemporary(rt.width, rt.height, 0, rt.graphicsFormat);
                Graphics.CopyTexture(tempRT, z * rt.width * rt.height, 0, sliceRT, 0 , 0); // 复制第 0 个 mip，第 0 个 slice (z) 到临时 RT

                // 读取像素数据
                Texture2D tempTex = new Texture2D(rt.width, rt.height, rt.graphicsFormat, TextureCreationFlags.None);
                tempTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tempTex.Apply();

                Color[] pixels = tempTex.GetPixels();

                // 将像素数据设置到 Texture3D 的对应深度层
                texture3D.SetPixels(pixels, z);

                // 清理临时资源
                DestroyImmediate(tempTex);
                RenderTexture.ReleaseTemporary(sliceRT);
            }

            RenderTexture.active = null;
            texture3D.Apply();

            // 销毁临时 RenderTexture
            tempRT.Release();

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
            output.Create();
            return output;
        }


        public Texture2D GetCrossSection( int channel, int zIndex) {
            RenderTexture _noiseTexture = noiseTexture;
            int size = _noiseTexture.width;
        
            noiseComputeShader.SetTexture(KERNEL_CROSS_SECTION_ID, "noiseTexture", _noiseTexture);
            RenderTexture crossSection = new RenderTexture(size, size, 0);

            crossSection.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            crossSection.enableRandomWrite = true;
            crossSection.Create();

            noiseComputeShader.SetTexture(KERNEL_CROSS_SECTION_ID, "crossSection", crossSection);
            noiseComputeShader.SetInt("zIndex", zIndex);
            int numThreadGroups = Mathf.CeilToInt(size / 32f);
            noiseComputeShader.Dispatch(KERNEL_CROSS_SECTION_ID, numThreadGroups, numThreadGroups, 1);

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