using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace ATGrassCloud
{
    [ExecuteInEditMode]
    public class GrassRenderDebuger : MonoBehaviour
    {
        public ATGrassData grassData;
        public bool enableDebug = false;


        public void LateUpdate()
        {
            // if ( enableDebug )
            // {
            //     if ( grassData == null )
            //     {
            //         return;
            //     }
            //     if ( GrassRenderPass.Instance == null )
            //     {
            //         return;
            //     }

            //     for (int i = 0; i < grassData.cascadeDataList.Count; i++)
            //     {
            //         var cascade = GrassRenderPass.Instance.GetCascade(i);

            //         if ( grassData.cascadeDataList[i].debugGrassPosition )
            //         {
            //             DrawGrassPosition(cascade);
            //         }
            //     }
            // }
        }

        // public void DrawGrassPosition( ATGrassCascade cascade  ) 
        // {
        //     // get position data 
        //     var datas = cascade.GetGrassData();

        //     if (datas != null)
        //     {
        //         for (int i = 0; i < datas.Length; i++)
        //         {
        //             if (i % 100 == 0 )
        //             {
        //                 // Debug.Log("Grass Position " + datas[i].position);
        //                 Debug.DrawRay(datas[i].position, Vector3.up * 0.5f, Color.red);
        //             }
        //             //Gizmos.color = Color.green;
        //             //Gizmos.DrawWireSphere(datas[i].position, 0.1f);
        //         }
        //     }
        // }

        public void OnDrawGizmos()
        {
            if ( !enableDebug )
            {
                return;
            }
            if (grassData == null)
            {
                return;
            }
            // if ( GrassRenderPass.Instance == null )
            // {
            //     return;
            // }

            //for (int i = 0; i < grassData.cascadeDataList.Count; i++)
            //{
            //    var cascade = GrassRenderPass.Instance.GetCascade(i);

            //    if (grassData.cascadeDataList[i].debugGrassPosition)
            //    {
            //        DrawGrassPosition(cascade);
            //    }
            //}
        }
    }

}