using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ATGrassCloud
{
    public enum CloudObjectType
    {
        None,
        BasicObject,
        Mesh,
    }

    public enum BasicCloudObjectType{

        None = 0,
        Sphere = 1,
        Box = 2,

    }

    public struct ATCloudObjectBuffer
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public float objectType;
        public float boundRadius;

        public Vector4 param;
    }

    [CreateAssetMenu(fileName = "ATCloudObjectData", menuName = "ATGrassCloud/Cloud Object Data" , order = 100)]


    public class ATCloudObjectData : ScriptableObject
    {
        [BoxGroup("Basic Info")]
        public string objectName = "CloudObject";
        [BoxGroup("Basic Info")]
        public CloudObjectType objectType = CloudObjectType.BasicObject;
        [BoxGroup("Basic Info")]
        public BasicCloudObjectType basicObjectType = BasicCloudObjectType.Sphere;

        // [BoxGroup("Data")]
        // public float objectSize = 1.0f;

        // [BoxGroup("Data")]
        // public float radius0;
        // [BoxGroup("Data")]
        // public float radius1;
        // [BoxGroup("Data")]
        // public float length0;
        // [BoxGroup("Data")]
        // public float length1;

        [BoxGroup("BoundingSphereScale")]
        public float boundingSphereScale = 1.0f;

        [BoxGroup("Object Data")]
        [ShowIf("basicObjectType",BasicCloudObjectType.Sphere)]
        public float radius;
        [BoxGroup("Object Data")]
        [ShowIf("basicObjectType",BasicCloudObjectType.Box)]
        public float length;


        public float GetBoundingSphereRadius()
        {
            if ( objectType == CloudObjectType.BasicObject )
            {
                switch (basicObjectType)
                {
                    case BasicCloudObjectType.Sphere:
                        return radius * boundingSphereScale;
                    case BasicCloudObjectType.Box:
                        return length * 0.5f * Mathf.Sqrt(3.0f) * boundingSphereScale;
                    default:
                        return  boundingSphereScale;
                }
            }else{

                return boundingSphereScale;
            }

        }

        public Vector4 GetParam()
        {
            if ( objectType == CloudObjectType.BasicObject )
            {
                switch (basicObjectType)
                {
                    case BasicCloudObjectType.Sphere:
                        return new Vector4(radius,0,0,0);
                    case BasicCloudObjectType.Box:
                        return new Vector4(length,0,0,0);
                    default:
                        return new Vector4(0,0,0,0);
                }
            }
            return Vector4.zero;

        }




        [BoxGroup("Debug")]
        public bool drawBoundingSphere = false;




    }
}
