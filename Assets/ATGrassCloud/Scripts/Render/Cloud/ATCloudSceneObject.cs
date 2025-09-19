using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ATGrassCloud
{
    [ExecuteInEditMode]
    public class ATCloudSceneObject : MonoBehaviour
    {
        [InlineEditor]
        public ATCloudObjectData data;

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