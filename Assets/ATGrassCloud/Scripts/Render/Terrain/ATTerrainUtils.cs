using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System;

namespace ATGrassCloud
{
    public static  class ATTerrainUtils
    {
        public static Mesh CreatePlaneMesh(int gridCountInRow , float gridSize){
            var mesh = new Mesh();
           
            var sizePerGrid = gridSize;
            var totalMeterSize = gridCountInRow * sizePerGrid;
            var gridCount = gridCountInRow * gridCountInRow;
            var triangleCount = gridCount * 2;

            var vOffset = - totalMeterSize * 0.5f;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            float uvStrip = 1f / gridCountInRow;
            for(var z = 0; z <= gridCountInRow;z ++){
                for(var x = 0; x <= gridCountInRow; x ++){
                    vertices.Add(new Vector3(vOffset + x * 0.5f,0,vOffset + z * 0.5f));
                    uvs.Add(new Vector2(x * uvStrip,z * uvStrip));
                }
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uvs);

            int[] indices = new int[triangleCount * 3];

            for(var gridIndex = 0; gridIndex < gridCount ; gridIndex ++){
                var offset = gridIndex * 6;
                var vIndex = (gridIndex / gridCountInRow) * (gridCountInRow + 1) + (gridIndex % gridCountInRow);

                indices[offset] = vIndex;
                indices[offset + 1] = vIndex + gridCountInRow + 1;
                indices[offset + 2] = vIndex + 1;
                indices[offset + 3] = vIndex + 1; 
                indices[offset + 4] = vIndex + gridCountInRow + 1;
                indices[offset + 5] = vIndex + gridCountInRow + 2;
            }
            mesh.SetIndices(indices,MeshTopology.Triangles,0);
            mesh.UploadMeshData(false);
            return mesh;
        }



         public static Mesh CreateCube(int  gridCount , float gridSize ){
            var mesh = new Mesh();
            List<Vector3> vertices = new List<Vector3>();
            float extent = gridCount * 0.5f;

            vertices.Add(new Vector3(-extent,-extent,-extent));
            vertices.Add(new Vector3(extent,-extent,-extent));
            vertices.Add(new Vector3(extent,extent,-extent));
            vertices.Add(new Vector3(-extent,extent,-extent));
            
            vertices.Add(new Vector3(-extent,extent,extent));
            vertices.Add(new Vector3(extent,extent,extent));
            vertices.Add(new Vector3(extent,-extent,extent));
            vertices.Add(new Vector3(-extent,-extent,extent));

            int[] indices = new int[6*6];

            int[] triangles = {
                0, 2, 1, //face front
                0, 3, 2,
                2, 3, 4, //face top
                2, 4, 5,
                1, 2, 5, //face right
                1, 5, 6,
                0, 7, 4, //face left
                0, 4, 3,
                5, 4, 7, //face back
                5, 7, 6,
                0, 6, 7, //face bottom
                0, 1, 6
            };

            mesh.SetVertices(vertices);
            mesh.triangles = triangles;
            mesh.UploadMeshData(false);
            return mesh;
        }

//================== Create RT ======================================


        public static RenderTexture CreateRenderTextureWithMipTextures(List<Texture2D> mipmaps,RenderTextureFormat format){
            var mip0 = mipmaps[0];
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(mip0.width,mip0.height,format,0,mipmaps.Count);
            descriptor.autoGenerateMips = false;
            descriptor.useMipMap = true;
            RenderTexture rt = new RenderTexture(descriptor);
            rt.filterMode = mip0.filterMode;
            rt.Create();
            for(var i = 0; i < mipmaps.Count; i ++){
                Graphics.CopyTexture(mipmaps[i],0,0,rt,0,i);
            }
            return rt;
        }

//=================== Create Mip Map Texture ======================================
        public static Texture2D ConvertToTexture2D(RenderTexture renderTexture,TextureFormat format){
            var original = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var tex = new Texture2D(renderTexture.width,renderTexture.height,format,0,false);
            tex.filterMode = renderTexture.filterMode;
            tex.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0,false);
            tex.Apply(false,false);
            RenderTexture.active = original;
            return tex;
        }

        // Calculate Height Map Min Max Height
        public static RenderTexture CreateMinMaxHeightTexture(int texSize){
            RenderTextureDescriptor desc = new RenderTextureDescriptor(texSize,texSize,RenderTextureFormat.RG32,0,1);
            desc.enableRandomWrite = true;
            desc.autoGenerateMips = false;
            var rt = RenderTexture.GetTemporary(desc);
            rt.filterMode = FilterMode.Point;
            rt.Create();
            // set initial value to min float
            Graphics.SetRenderTarget(rt);
            GL.Clear(true,true,Color.black);
            return rt;
        }

        public static void CalcuateGroupXY(ComputeShader computeShader, int kernelIndex,int textureSize,out int groupX,out int groupY){
            uint threadX,threadY,threadZ;
            computeShader.GetKernelThreadGroupSizes(kernelIndex,out threadX,out threadY,out threadZ);
            groupX = (int)( ( textureSize + threadX - 1) / threadX);
            groupY = (int)( ( textureSize + threadY - 1) / threadY);
        }


        public static void UpdateGPUAsyncRequest(AsyncGPUReadbackRequest req){
#if UNITY_EDITOR
            UnityEditor.EditorApplication.CallbackFunction callUpdate = null;
            callUpdate = ()=>{
                if(req.done){
                    return;
                }
                req.Update();
                UnityEditor.EditorApplication.delayCall += callUpdate;
            };
            callUpdate();
#endif
        }
        public static void WaitRenderTexture(RenderTexture renderTexture,System.Action<RenderTexture> callback){
            var request = AsyncGPUReadback.Request(renderTexture,0,TextureFormat.RG32,(res)=>{
                callback(renderTexture);
            });
            ATTerrainUtils.UpdateGPUAsyncRequest(request);    
        }


        public static string GetMipTexPath(string outDir, int mipIndex){
            var path = $"{outDir}/MinMaxHeight_{mipIndex}.png";
            return path;
        }

        public static void EnsureDir( string outDir , Texture2D heightmap ){
#if UNITY_EDITOR
            var heightMapPath = UnityEditor.AssetDatabase.GetAssetPath(heightmap);
            var dir = System.IO.Path.GetDirectoryName(heightMapPath);
            var heightMapName = System.IO.Path.GetFileNameWithoutExtension(heightMapPath);
            outDir = $"{dir}/{heightMapName}";
            if(!System.IO.Directory.Exists(outDir)){
                System.IO.Directory.CreateDirectory(outDir);
            }
#endif 
        }
        public static void SaveMipTextures(List<RenderTexture> mipTextures,string outDir){
#if UNITY_EDITOR
            for(var i = 0; i < mipTextures.Count; i ++){
                var path = GetMipTexPath(outDir,i);
                var tex2D = ATTerrainUtils.ConvertToTexture2D(mipTextures[i],TextureFormat.RG32);
                var bytes = tex2D.EncodeToPNG();
                System.IO.File.WriteAllBytes(path,bytes);
                UnityEngine.Object.DestroyImmediate(tex2D);
            }
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        static public void GenerateMipMaps(ComputeShader computeShader, int totalMips, List<RenderTexture> mipTextures,  System.Action callback){
            GenerateMipMap(computeShader,mipTextures[mipTextures.Count - 1],(mipTex)=>{
                mipTextures.Add(mipTex);
                if(mipTextures.Count < totalMips){
                    GenerateMipMaps(computeShader,totalMips,mipTextures,callback);
                }else{
                    callback();
                }
            });
        }

        public static void GeneratePatchMinMaxHeightTexMip0(Texture2D heightMap, ComputeShader computeShader, int patchTexSize, System.Action<RenderTexture> callback){
            int kernelIndex = 0;
            var minMaxHeightTex = CreateMinMaxHeightTexture(patchTexSize);
            int groupX,groupY;
            CalcuateGroupXY(computeShader,kernelIndex,patchTexSize,out groupX,out groupY);
            computeShader.SetTexture(kernelIndex,"HeightTex",heightMap);
            computeShader.SetTexture(kernelIndex,"PatchMinMaxHeightTex",minMaxHeightTex);
            computeShader.SetInt("texSize",heightMap.width);
            computeShader.Dispatch(kernelIndex,groupX,groupY,1);
            WaitRenderTexture(minMaxHeightTex,callback);
        }

        static public void Generate(string outDir,Texture2D heightMap,ComputeShader minMaxHeightsShader,int patchSize, int level){
            EnsureDir(outDir,heightMap);
            List<RenderTexture> textures = new List<RenderTexture>();

            GeneratePatchMinMaxHeightTexMip0(heightMap,minMaxHeightsShader,patchSize,(rt)=>{
                textures.Add(rt);
                GenerateMipMaps(minMaxHeightsShader,level,textures,()=>{
                    SaveMipTextures(textures,outDir);
                    foreach(var rt in textures){
                        RenderTexture.ReleaseTemporary(rt);
                    }
                });
            });
        }


        static public void GenerateMipMap(ComputeShader computeShader,RenderTexture inTex,System.Action<RenderTexture> callback){
            var kernelIndex = 1;
            var reduceTex = CreateMinMaxHeightTexture(inTex.width / 2);
            computeShader.SetTexture(kernelIndex,"InTex",inTex);
            computeShader.SetTexture(kernelIndex,"ReduceTex",reduceTex);
            computeShader.SetInt("texSize",inTex.width);
            int groupX,groupY;
            CalcuateGroupXY(computeShader,kernelIndex,reduceTex.width,out groupX,out groupY);
            computeShader.Dispatch(kernelIndex,groupX,groupY,1);
            WaitRenderTexture(reduceTex,callback);
        }


        public static void GenerateMinMaxHeightMapFromSelectedHeightMap( Texture2D texture,ComputeShader minMaxHeightsShader,int patchSize, int level ){
            if(texture is Texture2D heightMap){
#if UNITY_EDITOR
                var filePath = UnityEditor.AssetDatabase.GetAssetPath(heightMap);
                var dir = System.IO.Path.GetDirectoryName(filePath);
                var heightMapName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                dir = $"{dir}/{heightMapName}";
                Generate(dir,heightMap,minMaxHeightsShader,patchSize,level);
#endif
            }
        }

    }
}