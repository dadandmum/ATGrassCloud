using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace ATGrassCloud
{
    [ExecuteInEditMode]
    public class ATCloudSceneObject : MonoBehaviour
    {
        [InlineEditor]
        public ATCloudObjectData data;

        public static string path = "Assets/ATGrassCloud/Data/CloudObject/";

        [Button]
        public void GenerateNewCloudObject()
        {
#if UNITY_EDITOR
            // Make sure the target directory exists
            if (!AssetDatabase.IsValidFolder(path))
            {
                // Create the folder path if it doesn't exist
                string[] folders = path.TrimEnd('/').Split('/');
                string currentPath = "";
                foreach (string folder in folders)
                {
                    currentPath = Path.Combine(currentPath, folder);
                    if (!AssetDatabase.IsValidFolder(currentPath))
                    {
                        AssetDatabase.CreateFolder(currentPath == "Assets" ? "" : "Assets", folder);
                    }
                }
            }

            // Ensure the path ends with a slash for file creation
            string assetPath = path;
            if (!assetPath.EndsWith("/"))
                assetPath += "/";

            // Generate a unique filename (e.g., CloudObject_0.asset, CloudObject_1.asset, etc.)
            string baseName = "CloudObjectData";
            string fileName = baseName;
            string fullAssetPath = assetPath + fileName + ".asset";
            int index = 0;
            while (AssetDatabase.LoadAssetAtPath<ATCloudObjectData>(fullAssetPath) != null)
            {
                fileName = baseName + "_" + index;
                fullAssetPath = assetPath + fileName + ".asset";
                index++;
            }

            // Create a new instance of ATCloudObjectData
            ATCloudObjectData newCloudData = ScriptableObject.CreateInstance<ATCloudObjectData>();

            // Save the new asset to the project
            AssetDatabase.CreateAsset(newCloudData, fullAssetPath);

            // Optional: Initialize default values here
            // newCloudData.someProperty = defaultValue;

            // Save changes to disk
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(); // Ensure the asset is recognized

            // Select and ping the asset in the Project window
            EditorGUIUtility.PingObject(newCloudData);

            // Assign the newly created asset to the public data field
            data = newCloudData;

            Debug.Log($"New Cloud Object created: {fullAssetPath}");
#endif
        }

        public ATCloudObjectBuffer GetObjectBuffer()
        {
            ATCloudObjectBuffer buffer = new ATCloudObjectBuffer();
            buffer.position = transform.position;
            buffer.rotation = transform.rotation;
            buffer.scale = transform.lossyScale;
            if ( data.objectType == CloudObjectType.BasicObject)
                buffer.objectType = (float)(int)data.basicObjectType;
            else 
                buffer.objectType = 0;
            buffer.boundRadius = GetCloudObjectBoundingRadius();
            buffer.param = data.GetParam();
            return buffer;
        }

        public static ATCloudObjectBuffer GetDefaultObjectBuffer()
        {
            ATCloudObjectBuffer buffer = new ATCloudObjectBuffer();
            buffer.position = new Vector3(99999f, 999999f, 99999f);
            buffer.rotation = Quaternion.identity;
            buffer.scale = Vector3.one;
            buffer.boundRadius = 0;
            buffer.objectType = (float)BasicCloudObjectType.None;
            buffer.param = Vector4.zero;
            return buffer;
        }

        public float GetCloudObjectBoundingRadius()
        {
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(scale.x, scale.y, scale.z);
            
            return maxScale * data.GetBoundingSphereRadius();
        }

        public void OnEnable()
        {
            ATCloudObjectManager.Instance.AddCloudObject(this);
        }

        public void OnDisable() {
            if (ATCloudObjectManager.Instance != null)
                ATCloudObjectManager.Instance.RemoveCloudObject(this);
        }

        public void OnDestroy()
        {
            if (ATCloudObjectManager.Instance != null)
                ATCloudObjectManager.Instance.RemoveCloudObject(this);
        }


        public void  OnDrawGizmos() {
            if ( data.drawBoundingSphere)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, GetCloudObjectBoundingRadius());

            }
        }

    }
}