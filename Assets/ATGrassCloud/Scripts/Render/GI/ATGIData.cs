using Sirenix.OdinInspector;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;




#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif


[CreateAssetMenu(menuName = "ATGrassCloud/ATGIData" , order = 200)]
public class ATGIData : ScriptableObject
{
    [BoxGroup("SH Params")]
    public Vector3[] AT_SH_Params;
    [BoxGroup("SH Params")]
    public Vector3[] AT_SH_Params_Manually;

    [BoxGroup("Input")]
    public Texture2D inputTex2D;

    [BoxGroup("Input")]
    [Range(0.0f,5.0f)]
    public float intensity = 1.0f;


    [Button("Generate SH Tex2D")]
    public void ConvertTex2DTOSH()
    {
        // AT_SH_Params = BakeSHFromCubemapManually(convertedTex2D);
        // AT_SH_Params = BakeSHFromTex2DManually(inputTex2D);
        AT_SH_Params = BakeSHFromTex2D(inputTex2D, intensity);
        AT_SH_Params_Manually = BakeSHFromTex2DManually(inputTex2D, intensity);
    }


    // [BoxGroup("Input")]
    // public Cubemap inputCubemap;

    // [Button("Generate SH Cubemap")]
    // public void ConvertCubemapTOSH()
    // {
    //     // AT_SH_Params = BakeSHFromCubemapManually(convertedTex2D);
    //     AT_SH_Params = BakeSHFromCubemapManually(inputCubemap);
    // }


    private const int SAMPLE_COUNT = 10000; // 采样点数量（可调）
    private const int SH_COEFF_COUNT = 9;   // L0(1) + L1(3) + L2(5) = 9

    // SH 基函数系数（L2）
    private static float EvaluateSH(int l, int m, Vector3 dir)
    {
        float x = dir.x, y = dir.y, z = dir.z;

        switch (l)
        {
            case 0: // L0
                return 0.282095f; // 1/2√(π)

            case 1: // L1
                switch (m)
                {
                    case -1: return 0.488603f * y; // -Y
                    case 0: return 0.488603f * z;  // Z
                    case 1: return 0.488603f * x;  // X
                }
                break;

            case 2: // L2
                switch (m)
                {
                    case -2: return 1.092548f * x * y;
                    case -1: return 1.092548f * y * z;
                    case 0: return 0.946176f * (3 * z * z - 1); // (3z² - 1)
                    case 1: return 1.092548f * x * z;
                    case 2: return 0.546274f * (x * x - y * y);
                }
                break;
        }
        return 0;
    }

    public static Vector2 Direction2Texture2DUV(Vector3 dir)
    {
        float u = 0.5f + Mathf.Atan2(dir.x, -dir.z) * Mathf.Rad2Deg / 360;
        float v = 0.5f + Mathf.Asin(dir.y) * Mathf.Rad2Deg / 180;
        return new Vector2(u, v);
    }


    public static Vector3[] BakeSHFromTex2D(Texture2D texture2D, float intensity = 1.0f)
    {
        SphericalHarmonicsL2 sh = new SphericalHarmonicsL2();
        Vector3[] dirs = new Vector3[SAMPLE_COUNT];
        Color[] colors = new Color[SAMPLE_COUNT];
        for (int i = 0; i < SAMPLE_COUNT; i++)
        {
            // 均匀采样单位球方向（Marsaglia 方法）
            Vector3 dir;
            float x1, x2;
            do
            {
                x1 = UnityEngine.Random.value * 2 - 1;
                x2 = UnityEngine.Random.value * 2 - 1;
            } while (x1 * x1 + x2 * x2 >= 1);

            dir = new Vector3(
                2 * x1 * Mathf.Sqrt(1 - x1 * x1 - x2 * x2),
                2 * x2 * Mathf.Sqrt(1 - x1 * x1 - x2 * x2),
                1 - 2 * (x1 * x1 + x2 * x2)
            );

            // 从 Cubemap 采样颜色
            Vector2 uv = Direction2Texture2DUV(dir);
            // float size = texture2D.width;
            Color color = texture2D.GetPixelBilinear( uv.x, uv.y);

            // dirs[i] = dir;
            // colors[i] = color;

            sh.AddDirectionalLight(dir, color, 0.5f * Mathf.PI * intensity/ SAMPLE_COUNT);
        }


        // sh.Evaluate( dirs, colors);

        Vector3[] shRGB = new Vector3[SH_COEFF_COUNT];
        for (int i = 0; i < SH_COEFF_COUNT ; i++)
        {
            shRGB[i] = new Vector3(sh[0,i], sh[1,i], sh[2,i]);
        }
        return shRGB;
        
    }
        /// <summary>
    /// 手动从 Cubemap 烘焙 SH（蒙特卡洛积分）
    /// </summary>
    /// <param name="cubemap">输入 Cubemap</param>
    /// <returns>SH 系数数组（7 个 Vector3）</returns>
    public static Vector3[] BakeSHFromTex2DManually(Texture2D texture2D, float intensity = 1.0f)
    {
        Vector3[] shRGB = new Vector3[SH_COEFF_COUNT]; // 每个是 RGB

        for (int i = 0; i < SAMPLE_COUNT; i++)
        {
            // 均匀采样单位球方向（Marsaglia 方法）
            Vector3 dir;
            float x1, x2;
            do
            {
                x1 = UnityEngine.Random.value * 2 - 1;
                x2 = UnityEngine.Random.value * 2 - 1;
            } while (x1 * x1 + x2 * x2 >= 1);

            dir = new Vector3(
                2 * x1 * Mathf.Sqrt(1 - x1 * x1 - x2 * x2),
                2 * x2 * Mathf.Sqrt(1 - x1 * x1 - x2 * x2),
                1 - 2 * (x1 * x1 + x2 * x2)
            );

            // 从 Cubemap 采样颜色
            Vector2 uv = Direction2Texture2DUV(dir);
            // float size = texture2D.width;
            Color color = texture2D.GetPixelBilinear( uv.x, uv.y);
            // 投影到每个 SH 基函数
            for (int l = 0; l <= 2; l++)
            {
                for (int m = -l; m <= l; m++)
                {
                    int index = GetSHIndex(l, m);
                    float basis = EvaluateSH(l, m, dir);
                    shRGB[index] += new Vector3(color.r, color.g, color.b) * basis;
                }
            }
        }

        // 归一化（乘以 4π / N）
        float norm = 4 * Mathf.PI / SAMPLE_COUNT * intensity;
        for (int i = 0; i < SH_COEFF_COUNT ; i++)
        {
            shRGB[i] *= norm;
        }

        return shRGB;
    }


    
    // public static void DirectionToCubemapFaceAndUV(Vector3 direction, out CubemapFace face, out Vector2 uv)
    // {
    //     // 找到绝对值最大的坐标分量
    //     float absX = Mathf.Abs(direction.x);
    //     float absY = Mathf.Abs(direction.y);
    //     float absZ = Mathf.Abs(direction.z);
    //     float maxAxis = Mathf.Max(absX, Mathf.Max(absY, absZ));

    //     if (maxAxis == absX)
    //     {
    //         // X 轴是主要轴
    //         face = direction.x > 0 ? CubemapFace.PositiveX : CubemapFace.NegativeX;
    //         uv = new Vector2(-direction.z / absX, direction.y / absX);
    //     }
    //     else if (maxAxis == absY)
    //     {
    //         // Y 轴是主要轴
    //         face = direction.y > 0 ? CubemapFace.PositiveY : CubemapFace.NegativeY;
    //         uv = new Vector2(direction.x / absY, -direction.z / absY);
    //     }
    //     else
    //     {
    //         // Z 轴是主要轴
    //         face = direction.z > 0 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
    //         uv = new Vector2(-direction.x / absZ, direction.y / absZ);
    //     }

    //     // 将 UV 从 [-1, 1] 映射到 [0, 1]
    //     uv = new Vector2((uv.x + 1f) * 0.5f, (uv.y + 1f) * 0.5f);
    // }

    // /// <summary>
    // /// 手动从 Cubemap 烘焙 SH（蒙特卡洛积分）
    // /// </summary>
    // /// <param name="cubemap">输入 Cubemap</param>
    // /// <returns>SH 系数数组（7 个 Vector3）</returns>
    // public static Vector3[] BakeSHFromCubemapManually(Cubemap cubemap)
    // {
    //     Vector3[] shRGB = new Vector3[SH_COEFF_COUNT]; // 每个是 RGB

    //     for (int i = 0; i < SAMPLE_COUNT; i++)
    //     {
    //         // 均匀采样单位球方向（Marsaglia 方法）
    //         Vector3 dir;
    //         float x1, x2;
    //         do
    //         {
    //             x1 = UnityEngine.Random.value * 2 - 1;
    //             x2 = UnityEngine.Random.value * 2 - 1;
    //         } while (x1 * x1 + x2 * x2 >= 1);

    //         dir = new Vector3(
    //             2 * x1 * Mathf.Sqrt(1 - x1 * x1 - x2 * x2),
    //             2 * x2 * Mathf.Sqrt(1 - x1 * x1 - x2 * x2),
    //             1 - 2 * (x1 * x1 + x2 * x2)
    //         );

    //         // 从 Cubemap 采样颜色
    //         CubemapFace face;
    //         Vector2 uv;
    //         DirectionToCubemapFaceAndUV(dir, out face, out uv);

    //         float size = cubemap.width;
    //         Color color = cubemap.GetPixel(face, (int)(uv.x * size) , (int)(uv.y * size) );

    //         // 投影到每个 SH 基函数
    //         for (int l = 0; l <= 2; l++)
    //         {
    //             for (int m = -l; m <= l; m++)
    //             {
    //                 int index = GetSHIndex(l, m);
    //                 float basis = EvaluateSH(l, m, dir);
    //                 shRGB[index] += new Vector3(color.r, color.g, color.b) * basis;
    //             }
    //         }
    //     }

    //     // 归一化（乘以 4π / N）
    //     float norm = 4 * Mathf.PI / SAMPLE_COUNT;
    //     for (int i = 0; i < SH_COEFF_COUNT; i++)
    //     {
    //         shRGB[i] *= norm;
    //     }

    //     return shRGB;
    // }

    
    private static int GetSHIndex(int l, int m)
    {
        return l * l + l + m;
    }

}