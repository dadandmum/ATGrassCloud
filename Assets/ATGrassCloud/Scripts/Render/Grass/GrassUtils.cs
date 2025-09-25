using UnityEngine;
using System.Linq;

namespace ATGrassCloud
{
    public static class GrassUtils
    {
                // return Vector4 in format : (centerPos.x , centerPos.y , size , 1.0f / size )
        // size is the draw distance + snap distance
        public static Vector4 GetDrawTopDownTextureData( Camera camera , float drawDistance , float snapDistance )
        {
            Bounds cameraBounds = CalculateCameraBounds(camera, drawDistance + snapDistance);
            return GetDrawTopDownTextureData(cameraBounds, drawDistance, snapDistance);

            // var centerPos = GetCenterPosition(camera, snapDistance);
            // var size = drawDistance + snapDistance;
            // return new Vector4(centerPos.x , centerPos.y , size , 1.0f / Mathf.Max( 0.00001f , size));
        }

        public static Vector4 GetDrawTopDownTextureData( Bounds cameraBounds , float drawDistance , float snapDistance  )
        {
            Vector3 centerPos = GetCenterPosition(cameraBounds.center,snapDistance);
            float size =  (drawDistance + snapDistance) ;
            return new Vector4(centerPos.x , centerPos.y , size , 1.0f / Mathf.Max( 0.00001f , size));
        }

        public static Vector3 SnapCameraPosition( Vector3 pos  , float snapDistance )
        {
            Vector3 snappedPos = pos;
            snappedPos.x = Mathf.Round(pos.x / snapDistance) * snapDistance;
            snappedPos.z = Mathf.Round(pos.z / snapDistance) * snapDistance;
            return snappedPos;
        }


        
        //First thing is to calculate the new position of the camera
        //The "centerPos" refer to the XZ position of the camera while the Y position is the max.y of the calculated bounds
        //You can see that we are moving the camera in steps, cause we want the textures to not get updated until we move a certain threshold
        //if we let the camera move a lot we gonna have instability issues and a lot of flikering so we try to minimize that as much as possible
        // Vector2 centerPos = new Vector2(Mathf.Floor(camera.transform.position.x / textureUpdateThreshold) * textureUpdateThreshold, Mathf.Floor(Camera.main.transform.position.z / textureUpdateThreshold) * textureUpdateThreshold);
        public static Vector2 GetCenterPosition( Camera camera , float snapDistance )
        {
            return GetCenterPosition(camera.transform.position , snapDistance);
        }

        public static Vector2 GetCenterPosition( Vector3 pos , float snapDistance )
        {
            Vector3 snapPos = SnapCameraPosition(pos , snapDistance);
            Vector2 centerPos = new(snapPos.x, snapPos.z);
            return centerPos;
        }
        

        static public void CalculateTopDownCameraData( Camera camera , float drawDistance, float snapDistance , out Matrix4x4 viewMatrix , out Matrix4x4 projectionMatrix)
        {
            Bounds cameraBounds = CalculateCameraBounds(camera, drawDistance);
            Vector4 topDownData = GetDrawTopDownTextureData(cameraBounds, drawDistance, snapDistance);
            Vector3 cameraPos = new Vector3(topDownData.x, cameraBounds.max.y + 10f, topDownData.y);
            float size = topDownData.z;

            viewMatrix = Matrix4x4.TRS(cameraPos, Quaternion.LookRotation(-Vector3.up), new Vector3(1, 1, -1)).inverse;
            projectionMatrix = Matrix4x4.Ortho(- size , size, -size, size, 0, cameraBounds.size.y * 1.5f );
        }

        static public Bounds CalculateCameraBounds(Camera camera, float drawDistance)
        {
            Vector3 ntopLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.nearClipPlane));
            Vector3 ntopRight = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane));
            Vector3 nbottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane));
            Vector3 nbottomRight = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.nearClipPlane));

            Vector3 ftopLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, drawDistance));
            Vector3 ftopRight = camera.ViewportToWorldPoint(new Vector3(1, 1, drawDistance));
            Vector3 fbottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, drawDistance));
            Vector3 fbottomRight = camera.ViewportToWorldPoint(new Vector3(1, 0, drawDistance));

            float[] xValues = new float[] { ftopLeft.x, ftopRight.x, ntopLeft.x, ntopRight.x, fbottomLeft.x, fbottomRight.x, nbottomLeft.x, nbottomRight.x };
            float startX = xValues.Max();
            float endX = xValues.Min();

            float[] yValues = new float[] { ftopLeft.y, ftopRight.y, ntopLeft.y, ntopRight.y, fbottomLeft.y, fbottomRight.y, nbottomLeft.y, nbottomRight.y };
            float startY = yValues.Max();
            float endY = yValues.Min();

            float[] zValues = new float[] { ftopLeft.z, ftopRight.z, ntopLeft.z, ntopRight.z, fbottomLeft.z, fbottomRight.z, nbottomLeft.z, nbottomRight.z };
            float startZ = zValues.Max();
            float endZ = zValues.Min();

            Vector3 center = new Vector3((startX + endX) / 2, (startY + endY) / 2, (startZ + endZ) / 2);
            Vector3 size = new Vector3(Mathf.Abs(startX - endX), Mathf.Abs(startY - endY), Mathf.Abs(startZ - endZ));

            Bounds bounds = new Bounds(center, size);
            bounds.Expand(1);
            return bounds;
        }
    }
}
