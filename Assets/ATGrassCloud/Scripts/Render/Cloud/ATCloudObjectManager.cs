using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATGrassCloud
{
    [ExecuteInEditMode]
    public class ATCloudObjectManager : MonoBehaviour
    {

        public static ATCloudObjectManager Instance{
            get{
                if(m_instance == null){
                    m_instance = FindObjectOfType<ATCloudObjectManager>();
                }
                return m_instance;
            }

        }
        private static ATCloudObjectManager m_instance;
        public List<ATCloudSceneObject> cloudObjects = new List<ATCloudSceneObject>();


        public List<ATCloudSceneObject> GetCloudObjects()
        {
            if (cloudObjects == null){
                cloudObjects = new List<ATCloudSceneObject>();
            }

            return cloudObjects;
        }


        public void AddCloudObject(ATCloudSceneObject cloudObject)
        {
            if(cloudObjects == null){
                cloudObjects = new List<ATCloudSceneObject>();
            }

            if ( !cloudObjects.Contains(cloudObject)){
                cloudObjects.Add(cloudObject);
            }
        }

        public void RemoveCloudObject(ATCloudSceneObject cloudObject)
        {
            if(cloudObjects == null){
                return;
            }
            cloudObjects.Remove(cloudObject);
        }




    }
}