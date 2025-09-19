#ifndef QUATERNIONLIB_HLSL
#define QUATERNIONLIB_HLSL

/**
 * @brief Normalizes a quaternion to ensure it's a unit quaternion.
 * @param q Input quaternion (x, y, z, w)
 * @return Normalized quaternion
 */
float4 quatNormalize(float4 q)
{
    float magSqr = dot(q, q);                   // Compute squared magnitude
    if (magSqr == 0.0)                          // Avoid division by zero
        return float4(0, 0, 0, 1);              // Return identity if zero
    float invMag = 1.0 / sqrt(magSqr);          // Compute inverse magnitude
    return q * invMag;                          // Normalize and return
}

/**
 * @brief Computes the conjugate of a quaternion.
 *        For unit quaternions, this is equivalent to the inverse.
 * @param q Input quaternion
 * @return Conjugate: (-x, -y, -z, w)
 */
float4 quatConjugate(float4 q)
{
    return float4(-q.xyz, q.w);
}

/**
 * @brief Multiplies two quaternions: q * p
 *        Represents combined rotation (q then p)
 * @param q First quaternion
 * @param p Second quaternion
 * @return Resulting quaternion
 */
float4 quatMultiply(float4 q, float4 p)
{
    float4 result;
    // Scalar (w) part: w = qw*pw - dot(qv, pv)
    result.w = q.w * p.w - q.x * p.x - q.y * p.y - q.z * p.z;
    // Vector (xyz) part
    result.x = q.w * p.x + q.x * p.w + q.y * p.z - q.z * p.y;
    result.y = q.w * p.y - q.x * p.z + q.y * p.w + q.z * p.x;
    result.z = q.w * p.z + q.x * p.y - q.y * p.x + q.z * p.w;
    return result;
}

/**
 * @brief Rotates a 3D vector by a quaternion.
 *        Uses the formula: v' = q * v * q⁻¹
 * @param q Unit quaternion representing rotation
 * @param v Vector to rotate
 * @return Rotated vector in 3D space
 */
float3 quatRotate(float4 q, float3 v)
{
    float4 qv = float4(v, 0.0);           // Convert vector to pure quaternion [v, 0]
    float4 qInv = quatConjugate(q);       // Inverse = conjugate for unit quaternions
    float4 result = quatMultiply(quatMultiply(q, qv), qInv); // q * v * q⁻¹
    return result.xyz;                    // Return only the vector part
}


#endif