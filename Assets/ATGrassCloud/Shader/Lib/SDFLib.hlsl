#ifndef SDFLIB_HLSL
#define SDFLIB_HLSL


//======== Math ==========
float ndot(float2 a, float2 b)
{
    return a.x * b.x - a.y * b.y;
}

float dot2(float3 f)
{
    return dot(f, f);
}
			
float dot2(float2 f)
{
    return dot(f, f);
}

float sdUnion(float d1, float d2)
{
    return min(d1, d2);
}

float sdIntersection(float d1, float d2)
{
    return max(d1, d2);
}

float sdSubtraction(float d1, float d2)
{
    return max(-d1, d2);
}

float sdFMod(inout float p, float s)
{
    float h = s * .5f;
    float c = floor((p + h) / s);
    p = fmod(p + h, s) - h;
    p = fmod(-p + h, s) - h;
    return c;
}

float3 normalizeF3(float3 f)
{
    return (f * 0.5) + 0.5;
}


//===========SDF Function============

//sphere
float sdSphere(float3 posOS , float radius)
{
    return length(posOS) - radius;
}


//box
float sdBox(float3 posOS, float len)
{
    float3 q = abs(posOS) - len * 0.5;
    return length(max(q, 0)) + min(max(q.x, max(q.y, q.z)), 0);
}

//capsule
float sdCapsule(float3 p, float3 a, float3 b, float r)
{
    float3 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h) - r;
}


//capsule
float sdCapsule(float3 p, float len, float r)
{
    float3 top = float3( 0 , 0.5f * len, 0);
    float3 bottom = float3( 0 , -0.5f * len, 0);
    return sdCapsule(p, top, bottom, r);
}

//rounded cylinder
float sdRoundedCylinder(float3 p, float ra, float rb, float h)
{
    float2 d = float2(length(p.xz) - 2.0 * ra + rb, abs(p.y) - h);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - rb;
}

float sdCylinder(float3 p, float h, float r)
{
    // Half-height
    float halfH = h * 0.5f;

    // Radial distance in the XZ plane
    float2 w = float2(length(p.xz), p.y);  // (radial distance, y coordinate)

    // Vector from closest point on segment [-h/2, h/2] along Y to (radial, y)
    float2 d = float2(
        w.x - r,   // radial: distance from radius (positive if outside)
        abs(w.y) - halfH   // axial: distance from cylinder end (positive if beyond cap)
    );

    // Determine which feature we're closest to:
    // - If d.y <= 0, we're between the caps → distance is dominated by radial part (or inside)
    // - If d.x <= 0, we're inside radial bounds → capped by axial (end caps)
    float insideRadial = d.x < 0.0f;
    float insideAxial  = d.y < 0.0f;

    // Compute signed distance based on region:
    if (insideRadial && insideAxial)
    {
        // Inside both radial and axial bounds → we are inside the cylinder
        // The distance is negative of the maximum "penetration"
        return -min(-d.x, -d.y);
    }

    // Return length of positive components: this gives distance to nearest feature
    return length(max(d, 0.0f)) + min(0.0f, max(d.x, d.y));
}


// ============== SDF Detect Distance =======

/// @brief Intersect a ray with a sphere centered at the origin in object space.
/// @param posOS     Ray origin in object space.
/// @param viewOS    Ray direction in object space (does not need to be normalized).
/// @param radius    Radius of the sphere (assumed centered at origin).
/// @return          The closest intersection point in object space, 
///                  or float3(1e30) if no intersection.
float3 rayIntersectSphere(float3 posOS, float3 viewOS, float radius)
{
    // Ensure ray direction is meaningful
    float3 rayDir = normalize(viewOS);

    // Sphere is centered at origin (0,0,0) in object space
    float3 center = float3(0.0f, 0.0f, 0.0f);
    float3 L = posOS - center; // Vector from center to ray origin

    // Quadratic equation coefficients for: |o + t*d|^2 = r^2
    // Expanded: t^2*(d·d) + 2t*(d·L) + (L·L - r^2) = 0
    float a = dot(rayDir, rayDir); // = 1.0 if rayDir is normalized
    float b = 2.0f * dot(rayDir, L);
    float c = dot(L, L) - radius * radius;

    float discr = b * b - 4.0f * a * c; // Discriminant

    // No intersection if discriminant is negative
    if (discr < 0.0f)
    {
        return float3(1e30, 1e30, 1e30); // No hit
    }

    // Closest intersection distance
    float sqrtDiscr = sqrt(discr);
    float t1 = (-b - sqrtDiscr) / (2.0f * a);
    float t2 = (-b + sqrtDiscr) / (2.0f * a);

    // We want the closest **positive** intersection
    float t = 1e30;
    if (t1 >= 0.0f) t = t1;
    else if (t2 >= 0.0f) t = t2;

    // If both t1 and t2 are negative, the ray is inside or pointing away
    if (t == 1e30)
    {
        return float3(1e30, 1e30, 1e30); // No valid forward intersection
    }

    // Compute intersection point in object space
    float3 hitPoint = posOS + t * rayDir;

    return hitPoint;
}

/// @brief Intersect a ray with a cube centered at origin using explicit math.
/// @param posOS     Ray origin in object space: \vec{o}
/// @param viewOS    Ray direction in object space: \vec{d} (not necessarily normalized)
/// @param len       Side length of the cube (cube spans [-len/2, len/2] on each axis)
/// @return          Closest intersection point in object space, or (1e30) if no hit.
///
/// Math Background:
/// ----------------
/// The cube is defined as the intersection of 6 planes (a 3D interval):
///   x ∈ [c_x - s/2, c_x + s/2],  y ∈ [c_y - s/2, c_y + s/2],  z ∈ [c_z - s/2, c_z + s/2]
/// Since it's centered at origin: c = (0,0,0), so bounds are [-L, L] where L = len/2.
///
/// A ray is defined as: R(t) = o + t * d,  t ≥ 0
///
/// For each axis (x, y, z), we compute the values of t where the ray enters and exits
/// the "slab" (the region between two parallel planes). The intersection occurs only
/// where all three slabs overlap and t ≥ 0.
///
/// For axis k (e.g. x), the two planes are at:
///   p_min = -L,   p_max = +L
///
/// Solve for t:
///   o_k + t * d_k = p_min  →  t_min_k = (p_min - o_k) / d_k
///   o_k + t * d_k = p_max  →  t_max_k = (p_max - o_k) / d_k
///
/// But since d_k can be negative, t_min_k might be > t_max_k.
/// So we define:
///   t1_k = min(t_min_k, t_max_k)  → entry time for axis k
///   t2_k = max(t_min_k, t_max_k)  → exit time for axis k
///
/// The ray is inside the box when t ∈ [max(t1_x, t1_y, t1_z), min(t2_x, t2_y, t2_z)]
/// Let:
///   tEnter = max(t1_x, t1_y, t1_z)
///   tExit  = min(t2_x, t2_y, t2_z)
///
/// Intersection exists if: tEnter ≤ tExit AND tExit ≥ 0
/// The first hit is at t = tEnter if tEnter ≥ 0, otherwise at tExit (ray starts inside).
///
float3 rayIntersectBox(float3 posOS, float3 viewOS, float len)
{
    // ——————————————————————————————
    // Step 1: Define cube bounds
    // ——————————————————————————————
    float L = len * 0.5f; // Half-length: box spans [-L, L] on each axis
    float3 minBound = float3(-L, -L, -L); // Minimum corner
    float3 maxBound = float3( L,  L,  L); // Maximum corner

    // ——————————————————————————————
    // Step 2: Precompute inverse direction to avoid division in loop
    // ——————————————————————————————
    // We'll compute t = (plane_pos - o) / d  → same as (plane_pos - o) * (1/d)
    float3 invDir = float3(1.0f / viewOS.x, 1.0f / viewOS.y, 1.0f / viewOS.z);

    // ——————————————————————————————
    // Step 3: Compute intersection with each "slab" (axis-aligned pair of planes)
    // ——————————————————————————————
    // For each axis, compute t values where ray enters and exits the slab

    // X-axis slab: from x = -L to x = +L
    float t_min_x = (minBound.x - posOS.x) * invDir.x; // t when ray hits x = -L
    float t_max_x = (maxBound.x - posOS.x) * invDir.x; // t when ray hits x = +L

    // Ensure t1 ≤ t2 for X
    float t1_x = min(t_min_x, t_max_x);
    float t2_x = max(t_min_x, t_max_x);

    // Y-axis slab: from y = -L to y = +L
    float t_min_y = (minBound.y - posOS.y) * invDir.y; // t when ray hits y = -L
    float t_max_y = (maxBound.y - posOS.y) * invDir.y; // t when ray hits y = +L

    float t1_y = min(t_min_y, t_max_y);
    float t2_y = max(t_min_y, t_max_y);

    // Z-axis slab: from z = -L to z = +L
    float t_min_z = (minBound.z - posOS.z) * invDir.z; // t when ray hits z = -L
    float t_max_z = (maxBound.z - posOS.z) * invDir.z; // t when ray hits z = +L

    float t1_z = min(t_min_z, t_max_z);
    float t2_z = max(t_min_z, t_max_z);

    // ——————————————————————————————
    // Step 4: Find overlapping interval
    // ——————————————————————————————
    // Ray is inside box when t ∈ [tEnter, tExit]
    float tEnter = max(max(t1_x, t1_y), t1_z); // Latest entry time
    float tExit  = min(min(t2_x, t2_y), t2_z); // Earliest exit time

    // ——————————————————————————————
    // Step 5: Check for valid intersection
    // ——————————————————————————————
    // Valid if:
    //   - The entry happens before exit: tEnter <= tExit
    //   - The exit is in front of ray origin: tExit >= 0
    if (tEnter <= tExit && tExit >= 0.0f)
    {
        // Choose hit distance:
        // - If tEnter >= 0, ray hits from outside → use tEnter
        // - If tEnter < 0, ray starts inside box → use tExit (first surface it leaves)
        float tHit = (tEnter >= 0.0f) ? tEnter : tExit;

        // Compute hit point: P = origin + t * direction
        float3 hitPoint;
        hitPoint.x = posOS.x + tHit * viewOS.x;
        hitPoint.y = posOS.y + tHit * viewOS.y;
        hitPoint.z = posOS.z + tHit * viewOS.z;

        return hitPoint;
    }

    // ——————————————————————————————
    // No intersection
    // ——————————————————————————————
    return float3(1e30, 1e30, 1e30);
}



/// @brief Intersect a ray with a closed cylinder centered at origin, aligned with Y-axis.
///        Cylinder: radius r, height H → spans y ∈ [-H/2, H/2]
/// @param posOS     Ray origin in object space: \vec{o}
/// @param viewOS    Ray direction in object space: \vec{d} (not necessarily normalized)
/// @param height    Total height of the cylinder
/// @param radius    Radius of the cylinder
/// @return          Closest intersection point in object space, or (1e30) if no hit.
///
/// Math Derivation:
/// ----------------
/// The cylinder consists of:
///   A. Lateral Surface: x² + z² = r²,  y ∈ [-h, h],  h = height/2
///   B. Bottom Cap:     x² + z² ≤ r²,  y = -h
///   C. Top Cap:        x² + z² ≤ r²,  y = +h
///
/// Ray: R(t) = o + t * d
///
float3 rayIntersectCylinder(float3 posOS, float3 viewOS, float height, float radius)
{
    float h = height * 0.5f; // Half-height: cylinder from y = -h to y = +h
    float r2 = radius * radius;

    float3 o = posOS;
    float3 d = viewOS;

    // List of candidate hit distances
    float t_hit = 1e30;

    // ——————————————————————————————————————————————————————
    // Part 1: Intersect with lateral surface (curved side)
    //         Equation: x² + z² = r², for y ∈ [-h, h]
    //
    // Substitute: (o.x + t*d.x)^2 + (o.z + t*d.z)^2 = r^2
    // → t²*(dx² + dz²) + 2t*(ox*dx + oz*dz) + (ox² + oz² - r²) = 0
    // ——————————————————————————————————————————————————————

    float dx2_dz2 = d.x * d.x + d.z * d.z;
    float ox_dx__oz_dz = o.x * d.x + o.z * d.z;
    float ox2_oz2__r2 = o.x * o.x + o.z * o.z - r2;

    // If dx2_dz2 ≈ 0 → ray is parallel to Y-axis (no XZ movement)
    if (dx2_dz2 > 1e-8f)
    {
        // Quadratic: A*t² + B*t + C = 0
        float A_lat = dx2_dz2;
        float B_lat = 2.0f * ox_dx__oz_dz;
        float C_lat = ox2_oz2__r2;

        float discr = B_lat * B_lat - 4.0f * A_lat * C_lat;
        if (discr >= 0.0f)
        {
            float sqrtD = sqrt(discr);
            float t0 = (-B_lat - sqrtD) / (2.0f * A_lat);
            float t1 = (-B_lat + sqrtD) / (2.0f * A_lat);

            // Test both intersections
            for (int i = 0; i < 2; ++i)
            {
                float t = (i == 0) ? t0 : t1;
                if (t < 0.0f) continue;

                float3 P = o + t * d;
                // Check if y is within cylinder bounds
                if (P.y >= -h && P.y <= h)
                {
                    if (t < t_hit) t_hit = t;
                }
            }
        }
    }
    else
    {
        // Ray is nearly vertical (no change in x,z)
        // Only intersect if already on cylinder surface
        if (o.x * o.x + o.z * o.z <= r2 + 1e-4f && o.x * o.x + o.z * o.z >= r2 - 1e-4f)
        {
            // But only if moving inward? Not easy — skip unless needed.
            // For now, rely on caps.
        }
    }

    // ——————————————————————————————————————————————————————
    // Part 2: Intersect with bottom cap (y = -h)
    //         Condition: x² + z² ≤ r²
    // Solve: o.y + t*d.y = -h  → t = (-h - o.y) / d.y
    // ——————————————————————————————————————————————————————

    if (abs(d.y) > 1e-8f)
    {
        float t_bottom = (-h - o.y) / d.y;
        if (t_bottom >= 0.0f)
        {
            float3 P = o + t_bottom * d;
            float x = P.x, z = P.z;
            if (x*x + z*z <= r2)
            {
                if (t_bottom < t_hit) t_hit = t_bottom;
            }
        }
    }

    // ——————————————————————————————————————————————————————
    // Part 3: Intersect with top cap (y = +h)
    //         Condition: x² + z² ≤ r²
    // Solve: o.y + t*d.y = +h  → t = (h - o.y) / d.y
    // ——————————————————————————————————————————————————————

    float t_top = (h - o.y) / d.y;
    if (t_top >= 0.0f)
    {
        float3 P = o + t_top * d;
        float x = P.x, z = P.z;
        if (x*x + z*z <= r2)
        {
            if (t_top < t_hit) t_hit = t_top;
        }
    }

    // ——————————————————————————————————————————————————————
    // Final: Did we get any valid hit?
    // ——————————————————————————————————————————————————————

    if (t_hit < 1e30)
    {
        return o + t_hit * d;
    }

    // No intersection
    return float3(1e30, 1e30, 1e30);
}



/// @brief Intersect a ray with a capsule centered at origin, aligned along Y-axis.
///        Capsule: line segment from (0, -len/2, 0) to (0, +len/2, 0), radius = radius.
/// @param posOS     Ray origin in object space: \vec{o}
/// @param viewOS    Ray direction in object space: \vec{d} (does not need to be normalized)
/// @param len       Length of the cylindrical part (distance between sphere centers)
/// @param radius    Radius of the capsule
/// @return          Closest intersection point in object space, or (1e30) if no hit.
///
/// Math Derivation:
/// ----------------
/// The capsule is the set of points within distance `radius` from the line segment AB,
/// where A = (0, -h, 0), B = (0, +h, 0), h = len/2.
///
/// We break the problem into 3 parts:
///   1. Intersect with the infinite cylinder around AB
///   2. Clamp to segment (capsule body)
///   3. Intersect with hemispheres at A and B (caps)
/// Then take the closest valid hit.
///
float3 rayIntersectCapsule(float3 posOS, float3 viewOS, float len, float radius)
{
    float h = len * 0.5f; // Half-length: from center to sphere center

    float3 A = float3(0, -h, 0); // Bottom sphere center
    float3 B = float3(0,  h, 0); // Top sphere center

    // Normalize direction for cleaner math? No — keep t in world units
    float3 o = posOS;
    float3 d = viewOS;

    float3 AB = B - A;           // = (0, len, 0)
    float3 AO = o - A;
    float3 BO = o - B;

    float3 DO = d; // Direction vector

    // Precompute dot products of AB
    float ab2 = dot(AB, AB); // |AB|^2 = len^2
    float inv_ab2 = 1.0f / ab2;

    // List of candidate hit distances
    float t_hit = 1e30;

    // ——————————————————————————————————————————————————————
    // 1. Intersect with infinite cylinder of radius `radius` around line AB
    //    Equation: distance from point on ray to line AB = radius
    //
    // Let P(t) = o + t*d
    // Closest point on line AB to P(t): clamp( dot(AP, AB) / |AB|^2, 0, 1 )
    //
    // Vector from A to P(t): AP = AO + t*d
    // Project onto AB: s = dot(AP, AB) / |AB|^2
    //
    // Squared distance from P(t) to line AB:
    //   dist^2 = |AP|^2 - (dot(AP, AB))^2 / |AB|^2
    // Set equal to radius^2 → quadratic in t
    // ——————————————————————————————————————————————————————

    float ddo = dot(DO, AB);      // d·AB
    float ado = dot(AO, AB);      // AO·AB
    float aao = dot(AO, AO);      // |AO|^2
    float r2 = radius * radius;

    // Coefficients of quadratic: At^2 + Bt + C = 0
    float A_cyl = dot(DO, DO) - ddo * ddo * inv_ab2;
    float B_cyl = 2.0f * (dot(DO, AO) - ddo * ado * inv_ab2);
    float C_cyl = aao - ado * ado * inv_ab2 - r2;

    float discr_cyl = B_cyl * B_cyl - 4.0f * A_cyl * C_cyl;

    if (discr_cyl >= 0.0f && A_cyl != 0.0f)
    {
        float sqrtD = sqrt(discr_cyl);
        float t0 = (-B_cyl - sqrtD) / (2.0f * A_cyl);
        float t1 = (-B_cyl + sqrtD) / (2.0f * A_cyl);

        // Check both solutions
        for (int i = 0; i < 2; ++i)
        {
            float t = (i == 0) ? t0 : t1;
            if (t < 0.0f) continue;

            float3 P = o + t * d;                    // Point on ray
            float3 AP = P - A;
            float s = dot(AP, AB) * inv_ab2;         // Parametric coord along AB

            // Is the closest point on the segment AB?
            if (s >= 0.0f && s <= 1.0f)
            {
                if (t < t_hit) t_hit = t;
            }
        }
    }

    // ——————————————————————————————————————————————————————
    // 2. Intersect with bottom hemisphere (centered at A, radius = radius)
    //    But only the hemisphere pointing downward (y <= -h)
    //    Solve: |o + t*d - A|^2 = radius^2
    // ——————————————————————————————————————————————————————

    float3 OA = AO; // o - A
    float a = dot(DO, DO);
    float b = 2.0f * dot(DO, OA);
    float c = dot(OA, OA) - r2;

    float discr_sphereA = b * b - 4.0f * a * c;
    if (discr_sphereA >= 0.0f)
    {
        float sqrtD = sqrt(discr_sphereA);
        float t0 = (-b - sqrtD) / (2.0f * a);
        float t1 = (-b + sqrtD) / (2.0f * a);

        // Test t0 (closest)
        if (t0 >= 0.0f)
        {
            float3 P = o + t0 * d;
            if (P.y <= A.y) // On bottom hemisphere
            {
                if (t0 < t_hit) t_hit = t0;
            }
        }
        // If t0 not valid, try t1 (farther hit)
        else if (t1 >= 0.0f)
        {
            float3 P = o + t1 * d;
            if (P.y <= A.y)
            {
                if (t1 < t_hit) t_hit = t1;
            }
        }
    }

    // ——————————————————————————————————————————————————————
    // 3. Intersect with top hemisphere (centered at B, radius = radius)
    //    Only accept points where y >= B.y
    // ——————————————————————————————————————————————————————

    float3 OB = o - B;
    b = 2.0f * dot(DO, OB);
    c = dot(OB, OB) - r2;

    float discr_sphereB = b * b - 4.0f * a * c;
    if (discr_sphereB >= 0.0f)
    {
        float sqrtD = sqrt(discr_sphereB);
        float t0 = (-b - sqrtD) / (2.0f * a);
        float t1 = (-b + sqrtD) / (2.0f * a);

        if (t0 >= 0.0f)
        {
            float3 P = o + t0 * d;
            if (P.y >= B.y) // On top hemisphere
            {
                if (t0 < t_hit) t_hit = t0;
            }
        }
        else if (t1 >= 0.0f)
        {
            float3 P = o + t1 * d;
            if (P.y >= B.y)
            {
                if (t1 < t_hit) t_hit = t1;
            }
        }
    }

    // ——————————————————————————————————————————————————————
    // Final: Did we get any hit?
    // ——————————————————————————————————————————————————————

    if (t_hit < 1e30)
    {
        return o + t_hit * d;
    }

    // No intersection
    return float3(1e30, 1e30, 1e30);
}


// ========= Appendix =====================
//torus
float sdTorus(float3 p, float2 s)
{
    p = float3(p.x, p.z, -p.y);
    float2 w = float2(length(p.xz) - s.x, p.y);
    return length(w) - s.y;
}

//capped torus
float sdCappedTorus(float3 p, float ro, float ri, float2 t)
{
    p.x = abs(p.x);
    float x = (t.y * p.x > t.x * p.y) ? dot(p.xy, t) : length(p.xy);
    return sqrt(dot(p, p) + ro * ro - 2 * ro * x) - ri;
}

//link
float sdLink(float3 p, float s, float ro, float ri)
{
    float3 q = float3(p.x, max(abs(p.y) - s, 0), p.z);
    return length(float2(length(q.xy) - ro, q.z)) - ri;
}

//cone
float sdCone(float3 p, float2 c, float h)
{
    p -= float3(0, h / 2, 0);
    float2 q = h * float2(c.x / c.y, -1.0);

    float2 w = float2(length(p.xz), p.y);
    float2 a = w - q * clamp(dot(w, q) / dot(q, q), 0.0, 1.0);
    float2 b = w - q * float2(clamp(w.x / q.x, 0.0, 1.0), 1.0);
    float k = sign(q.y);
    float d = min(dot(a, a), dot(b, b));
    float s = max(k * (w.x * q.y - w.y * q.x), k * (w.y - q.y));
    return sqrt(d) * sign(s);
}

//infinite cone
float sdInfCone(float3 p, float2 c)
{
    float2 q = float2(length(p.xz), -p.y);
    float d = length(q - c * max(dot(q, c), 0));
    return d * ((q.x * c.y - q.y * c.x < 0) ? -1 : 1);
}

			//plane
float sdPlane(float3 p, float3 n, float h)
{
    return dot(p, n) + h;
}

			//hexagonal prism
float sdHexPrism(float3 p, float2 h)
{
    const float3 k = float3(-0.8660254, 0.5, 0.57735);
    p = abs(p);
    p.xy -= 2.0 * min(dot(k.xy, p.xy), 0.0) * k.xy;
    float2 d = float2(
					length(p.xy - float2(clamp(p.x, -k.z * h.x, k.z * h.x), h.x)) * sign(p.y - h.x),
					p.z - h.y);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

			//triangular prism
float sdTriPrism(float3 p, float2 h)
{
    float3 q = abs(p);
    return max(q.z - h.y, max(q.x * 0.866025 + p.y * 0.5, -p.y) - h.x * 0.5);
}

		
			//infinite cylinder
float sdInfiniteCylinder(float3 p, float3 c)
{
    return length(p.xz - c.xy) - c.z;
}



//round box 
float sdRoundBox(float3 p, float s, float t)
{
    float3 q = abs(p) - s;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - t;
}



			//capped cone
float sdCappedCone(float3 p, float h, float r1, float r2)
{
    p -= float3(0, h / 2, 0);
    float2 q = float2(length(p.xz), p.y);
    float2 k1 = float2(r2, h);
    float2 k2 = float2(r2 - r1, 2.0 * h);
    float2 ca = float2(q.x - min(q.x, (q.y < 0.0) ? r1 : r2), abs(q.y) - h);
    float2 cb = q - k1 + k2 * clamp(dot(k1 - q, k2) / dot2(k2), 0.0, 1.0);
    float s = (cb.x < 0.0 && ca.y < 0.0) ? -1.0 : 1.0;
    return s * sqrt(min(dot2(ca), dot2(cb)));
}

			//box frame
float sdBoxFrame(float3 p, float3 s, float t)
{
    p = abs(p) - s;
    float3 q = abs(p + t) - t;
    return min(min(
					length(max(float3(p.x, q.y, q.z), 0.0)) + min(max(p.x, max(q.y, q.z)), 0.0),
					length(max(float3(q.x, p.y, q.z), 0.0)) + min(max(q.x, max(p.y, q.z)), 0.0)),
					length(max(float3(q.x, q.y, p.z), 0.0)) + min(max(q.x, max(q.y, p.z)), 0.0));
}

			//solid angle
float sdSolidAngle(float3 p, float2 c, float ra)
{
    float2 q = float2(length(p.xz), p.y);
    float l = length(q) - ra;
    float m = length(q - c * clamp(dot(q, c), 0.0, ra));
    return max(l, m * sign(c.y * q.x - c.x * q.y));
}

			//cut sphere
float sdCutSphere(float3 p, float r, float h)
{
    float w = sqrt(r * r - h * h);

    float2 q = float2(length(p.xz), p.y);
    float s = max((h - r) * q.x * q.x + w * w * (h + r - 2.0 * q.y), h * q.x - w * q.y);
    return (s < 0.0) ? length(q) - r :
					(q.x < w) ? h - q.y :
					length(q - float2(w, h));
}

			//cut hollow sphere
float sdCutHollowSphere(float3 p, float r, float h, float t)
{
    float w = sqrt(r * r - h * h);
    float2 q = float2(length(p.xz), p.y);
    return ((h * q.x < w * q.y) ? length(q - float2(w, h)) :
					abs(length(q) - r)) - t;
}

			//death star
float sdDeathStar(float3 p, float ra, float rb, float d)
{
    float a = (ra * ra - rb * rb + d * d) / (2.0 * d);
    float b = sqrt(max(ra * ra - a * a, 0.0));

    float2 p2 = float2(p.x, length(p.yz));
    if (p2.x * b - p2.y * a > d * max(b - p2.y, 0.0))
        return length(p2 - float2(a, b));
    else
        return max((length(p2) - ra),
						-(length(p2 - float2(d, 0)) - rb));
}

			//round cone
float sdRoundCone(float3 p, float r1, float r2, float h)
{
    float b = (r1 - r2) / h;
    float a = sqrt(1.0 - b * b);
    float2 q = float2(length(p.xz), p.y);
    float k = dot(q, float2(-b, a));
    if (k < 0.0)
        return length(q) - r1;
    if (k > a * h)
        return length(q - float2(0.0, h)) - r2;
    return dot(q, float2(a, b)) - r1;
}

			//ellipsoid
float sdEllipsoid(float3 p, float3 r)
{
    float k0 = length(p / r);
    float k1 = length(p / (r * r));
    return k0 * (k0 - 1.0) / k1;
}

			//rhombus
float sdRhombus(float3 p, float la, float lb, float h, float ra)
{
    p = float3(p.x, p.z, -p.y);
    p = abs(p);
    float2 b = float2(la, lb);
    float f = clamp((ndot(b, b - 2.0 * p.xz)) / dot(b, b), -1.0, 1.0);
    float2 q = float2(length(p.xz - 0.5 * b * float2(1.0 - f, 1.0 + f)) * sign(p.x * b.y + p.z * b.x - b.x * b.y) - ra, p.y - h);
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0));
}

			//octahedron
float sdOctahedron(float3 p, float s)
{
    p = abs(p);
    float m = p.x + p.y + p.z - s;
    float3 q;
    if (3.0 * p.x < m)
        q = p.xyz;
    else if (3.0 * p.y < m)
        q = p.yzx;
    else if (3.0 * p.z < m)
        q = p.zxy;
    else
        return m * 0.57735027;

    float k = clamp(0.5 * (q.z - q.y + s), 0.0, s);
    return length(float3(q.x, q.y - s + k, q.z - k));
}

			//pyramid
float sdPyramid(float3 p, float h)
{
    p += float3(0, h / 2, 0);
    float m2 = h * h + 0.25;

    p.xz = abs(p.xz);
    p.xz = (p.z > p.x) ? p.zx : p.xz;
    p.xz -= 0.5;

    float3 q = float3(p.z, h * p.y - 0.5 * p.x, h * p.x + 0.5 * p.y);

    float s = max(-q.x, 0.0);
    float t = clamp((q.y - 0.5 * p.z) / (m2 + 0.25), 0.0, 1.0);

    float a = m2 * (q.x + s) * (q.x + s) + q.y * q.y;
    float b = m2 * (q.x + 0.5 * t) * (q.x + 0.5 * t) + (q.y - m2 * t) * (q.y - m2 * t);

    float d2 = min(q.y, -q.x * m2 - q.y * 0.5) > 0.0 ? 0.0 : min(a, b);

    return sqrt((d2 + q.z * q.z) / m2) * sign(max(q.z, -p.y));
}

			//triangle
float udTriangle(float3 p, float3 a, float3 b, float3 c)
{
    float3 ba = b - a;
    float3 pa = p - a;
    float3 cb = c - b;
    float3 pb = p - b;
    float3 ac = a - c;
    float3 pc = p - c;
    float3 nor = cross(ba, ac);

    return sqrt(
					(sign(dot(cross(ba, nor), pa)) +
						sign(dot(cross(cb, nor), pb)) +
						sign(dot(cross(ac, nor), pc)) < 2.0)
					?
					min(min(
						dot2(ba * clamp(dot(ba, pa) / dot2(ba), 0.0, 1.0) - pa),
						dot2(cb * clamp(dot(cb, pb) / dot2(cb), 0.0, 1.0) - pb)),
						dot2(ac * clamp(dot(ac, pc) / dot2(ac), 0.0, 1.0) - pc))
					:
					dot(nor, pa) * dot(nor, pa) / dot2(nor));
}

			//quad
float udQuad(float3 p, float3 a, float3 b, float3 c, float3 d)
{
    float3 ba = b - a;
    float3 pa = p - a;
    float3 cb = c - b;
    float3 pb = p - b;
    float3 dc = d - c;
    float3 pc = p - c;
    float3 ad = a - d;
    float3 pd = p - d;
    float3 nor = cross(ba, ad);

    return sqrt(
					(sign(dot(cross(ba, nor), pa)) +
						sign(dot(cross(cb, nor), pb)) +
						sign(dot(cross(dc, nor), pc)) +
						sign(dot(cross(ad, nor), pd)) < 3.0)
					?
					min(min(min(
						dot2(ba * clamp(dot(ba, pa) / dot2(ba), 0.0, 1.0) - pa),
						dot2(cb * clamp(dot(cb, pb) / dot2(cb), 0.0, 1.0) - pb)),
						dot2(dc * clamp(dot(dc, pc) / dot2(dc), 0.0, 1.0) - pc)),
						dot2(ad * clamp(dot(ad, pd) / dot2(ad), 0.0, 1.0) - pd))
					:
					dot(nor, pa) * dot(nor, pa) / dot2(nor));
}

			//fractal
float sdFractal(float3 z, float i, float s, float o)
{
    int n = 0;
    while (n < i)
    {
        if (z.x + z.y < 0)
            z.xy = -z.yx;
        if (z.x + z.z < 0)
            z.xz = -z.zx;
        if (z.y + z.z < 0)
            z.zy = -z.yz;
        z = z * s - o * (s - 1.0);
        n++;
    }
    return (length(z)) * pow(s, -float(n));
}

//tesseract
float sdTesseract(float3 p, float wPos, float4 s, float3 wRot)
{
    float4 p4 = float4(p, wPos);

    p4.xz = mul(p4.xz, float2x2(cos(wRot.y), sin(wRot.y), -sin(wRot.y), cos(wRot.y)));
    p4.yz = mul(p4.yz, float2x2(cos(wRot.x), -sin(wRot.x), sin(wRot.x), cos(wRot.x)));
    p4.xy = mul(p4.xy, float2x2(cos(wRot.z), -sin(wRot.z), sin(wRot.z), cos(wRot.z)));
    p4.xw = mul(p4.xw, float2x2(cos(wRot.x), sin(wRot.x), -sin(wRot.x), cos(wRot.x)));
    p4.zw = mul(p4.zw, float2x2(cos(wRot.z), -sin(wRot.z), sin(wRot.z), cos(wRot.z)));
    p4.yw = mul(p4.yw, float2x2(cos(wRot.y), -sin(wRot.y), sin(wRot.y), cos(wRot.y)));

    float4 d = abs(p4) - s;
    return min(max(d.x, max(d.y, max(d.z, d.w))), 0.0) + length(max(d, 0.0));
}


#endif
