#ifndef CLOUD_UTIL_HLSL
#define CLOUD_UTIL_HLSL


/**
 * @brief Transforms a world-space point into the local (object) space
 *        of an object defined by position, rotation (quaternion), and scale.
 * 
 * Process:
 * 1. Translate: worldPos - objectPos
 * 2. Rotate:  Apply inverse rotation (conjugate quaternion)
 * 3. Scale:   Divide by object scale (component-wise)
 *
 * @param worldPos    Point in world space
 * @param objectPos   Object's world position
 * @param objectRot   Object's rotation as quaternion (x,y,z,w)
 * @param objectScale Object's scale vector (sx, sy, sz)
 * @return The point in object's local space
 */
float3 WorldToModelSpace(
    float3 worldPos,
    float3 objectPos,
    float4 objectRot,
    float3 objectScale)
{
    // Step 1: Inverse Translation
    // Move point relative to object origin
    float3 offset = worldPos - objectPos;

    // Step 2: Inverse Rotation
    // Rotate the offset vector by the inverse (conjugate) of the object's rotation
    float4 qInv = quatConjugate(objectRot);
    float3 rotated = quatRotate(qInv, offset);

    // Step 3: Inverse Scaling
    // Divide by scale to cancel object's scale (prevent divide-by-zero)
    float3 epsilon = float3(1e-6, 1e-6, 1e-6);           // Small value to avoid division by zero
    float3 scaleSafe = max(abs(objectScale), epsilon);   // Clamp scale to minimum safe value
    float3 localPos = rotated / scaleSafe;               // Apply inverse scale

    return localPos;
}

/// @brief Transforms a direction vector from world space to object (model) space.
/// @param worldDir     The input direction vector in world space.
/// @param objectRot    Object's rotation as a quaternion (w, x, y, z), 
///                     representing rotation from object space to world space.
/// @param objectScale  Object's non-uniform scale along each axis (x, y, z).
/// @return             The direction vector in object space.
/// @note               Assumes objectRot is a **unit quaternion**.
///                     Handles near-zero scale to avoid division by zero.
///                     Result is not automatically normalized.
float3 WorldToModelDir(
    float3 worldDir,
    float4 objectRot,
    float3 objectScale)
{
    // Step 1: Inverse Rotation
    // Since objectRot transforms from object space to world space,
    // its conjugate (inverse for unit quaternions) transforms from world to object space.
    float4 qInv = quatConjugate(objectRot);
    float3 localDir = quatRotate(qInv, worldDir);

    // Step 2: Inverse Scaling
    // Direction vectors are scaled during transformation, so we divide by the object's scale
    // to reverse the effect. Avoid division by zero by clamping scale to a small epsilon.
    float3 epsilon = float3(1e-6f, 1e-6f, 1e-6f);
    float3 safeScale = max(abs(objectScale), epsilon);  // Prevent division by zero
    localDir = localDir / safeScale;

    // Note: The resulting direction vector is not normalized.
    // If a unit vector is required (e.g., for lighting), caller should normalize it:
    // e.g., localDir = normalize(localDir);

    return localDir;
}

/// @brief Transforms a point from object (model) space to world space.
/// @param modelPos     Input point in object space.
/// @param objectPos    Object's world position (translation).
/// @param objectRot    Object's rotation as a quaternion (w, x, y, z), 
///                     representing rotation from object space to world space.
/// @param objectScale  Object's non-uniform scale along each axis (x, y, z).
/// @return             The transformed point in world space.
/// @note               Assumes objectRot is a **unit quaternion**.
float3 ModelToWorldSpace(
    float3 modelPos,
    float3 objectPos,
    float4 objectRot,
    float3 objectScale)
{
    // Step 1: Apply Scaling
    // Scale the model-space point
    float3 scaled = modelPos * objectScale;

    // Step 2: Apply Rotation
    // Rotate the scaled point using the object's rotation quaternion
    float3 rotated = quatRotate(objectRot, scaled);

    // Step 3: Apply Translation
    // Translate the rotated point to its world position
    float3 worldPos = objectPos + rotated;

    return worldPos;
}



/**
 * @brief Computes the distance from the ray origin to the first intersection with a sphere.
 *
 * This function analytically solves for the intersection between a ray and a sphere
 * in world space. The ray is defined by an origin point and a normalized direction vector.
 * The sphere is defined by its center position and radius.
 *
 * @param posWS    Ray origin in world space.
 * @param viewDir  Ray direction in world space (assumed to be normalized).
 * @param objPOS   Sphere center position in world space.
 * @param radius   Sphere radius (must be positive).
 *
 * @return
 *   - A positive float: distance to the first intersection point on the sphere surface.
 *   - -1.0f: if the ray origin is inside the sphere.
 *   - 1e30f (approximates INF): if the ray does not intersect the sphere.
 *
 * @note
 *   - This function assumes `viewDir` is normalized. If not, results will be incorrect.
 *   - Uses a quadratic equation solver approach for exact analytical intersection.
 *   - Small epsilon (1e-5f) is used to avoid self-intersection due to floating-point precision.
 *   - If the ray starts inside the sphere, returns -1 regardless of direction.
 */
float RaySphereIntersect(float3 posWS, float3 viewDir, float3 objCenter, float radius)
{
    // Vector from sphere center to ray origin
    float3 L = posWS - objCenter;
    
    // Ray equation: P(t) = posWS + t * viewDir
    // Sphere equation: |P(t) - objCenter|^2 = radius^2
    // Substituting gives a quadratic in t: t^2 * |viewDir|^2 + 2*t*(L 路 viewDir) + |L|^2 - radius^2 = 0
    
    // Assuming viewDir is normalized (typical for camera rays)
    // So |viewDir|^2 = 1.0
    float a = 1.0f;
    float b = 2.0f * dot(L, viewDir);
    float c = dot(L, L) - radius * radius;
    
    float discriminant = b * b - 4.0f * a * c;
    
    // No real roots 鈫?no intersection
    if (discriminant < 0.0f)
    {
        return 1e30f; // Return INF (no hit)
    }
    
    // Compute the two intersection points
    float sqrtD = sqrt(discriminant);
    float t1 = (-b - sqrtD) * 0.5f; // Closer intersection
    float t2 = (-b + sqrtD) * 0.5f; // Farther intersection
    
    // Check if ray origin is inside the sphere
    float distToCenter = length(L);
    if (distToCenter < radius)
    {
        return -1.0f; // Inside the sphere
    }
    
    // Origin is outside the sphere: find the first valid forward intersection
    if (t1 > 1e-5f) // Avoid self-intersection due to floating-point error
    {
        return t1; // Hit the front surface
    }
    else if (t2 > 1e-5f)
    {
        return t2; // Ray passes through the back (e.g., entering from behind)
    }
    else
    {
        // Both intersections are behind the ray origin 鈫?no hit
        return 1e30f; // Return INF
    }
}

float InOutEaseCubic( float x )
{
    return x * x * ( 3.0 - 2.0 * x );
}


#endif 