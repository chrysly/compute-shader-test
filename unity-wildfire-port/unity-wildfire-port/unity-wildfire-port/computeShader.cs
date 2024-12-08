#version 430 core

layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

// ----------------------------------------------------------------------------
//
// uniforms
//
// ----------------------------------------------------------------------------

layout(rgba32f, binding = 0) uniform image3D velocityDensityTexture;
layout(rgba32f, binding = 1) uniform image3D pressureTempPhiReactionTexture;
layout(rgba32f, binding = 2) uniform image3D curlObstaclesTexture;

layout(location = 0) uniform float iTime;

uniform vec3 m_size;
uniform int m_iterations;
uniform float dt;
uniform float m_vorticityStrength;
uniform float m_densityAmount;
uniform float m_densityDissipation;
uniform float m_densityBuoyancy;
uniform float m_densityWeight;
uniform float m_temperatureAmount;
uniform float m_temperatureDissipation;
uniform float m_reactionAmount;
uniform float m_reactionDecay;
uniform float m_reactionExtinguishment;
uniform float m_velocityDissipation;
uniform float m_inputRadius;
uniform float m_ambientTemperature;
uniform vec3 m_inputPos;

// ----------------------------------------------------------------------------
//
// functions
//
// ----------------------------------------------------------------------------

vec4 GetVelocityDensityData(vec3 voxelCoord)
{
    ivec3 floorVoxelCoord = ivec3(voxelCoord);
    return imageLoad(velocityDensityTexture, floorVoxelCoord);
}

vec4 GetPressureTempPhiReactionData(vec3 voxelCoord)
{
    ivec3 floorVoxelCoord = ivec3(voxelCoord);
    return imageLoad(pressureTempPhiReactionTexture, floorVoxelCoord);
}

vec4 GetCurlObstaclesData(vec3 voxelCoord)
{
    ivec3 floorVoxelCoord = ivec3(voxelCoord);
    return imageLoad(curlObstaclesTexture, floorVoxelCoord);
}

void ComputeObstacles(vec3 voxelCoord, inout vec4 curlObstaclesData)
{
    float obstacle = 0;

    if (voxelCoord.x - 1 < 0)
    {
        obstacle = 1;
    }
    if (voxelCoord.x + 1 > m_size.x - 1)
    {
        obstacle = 1;
    }

    if (voxelCoord.y - 1 < 0)
    {
        obstacle = 1;
    }
    if (voxelCoord.y + 1 > m_size.y - 1)
    {
        obstacle = 1;
    }

    if (voxelCoord.z - 1 < 0)
    {
        obstacle = 1;
    }
    if (voxelCoord.z + 1 > m_size.z - 1)
    {
        obstacle = 1;
    }

    vec3 curl = curlObstaclesData.xyz;
    curlObstaclesData = vec4(curl, obstacle);
}

vec3 GetAdvectedPosCoords(vec3 voxelCoord, in vec4 velocityDensityData)
{
    voxelCoord -= dt * 1.0f * velocityDensityData.xyz;
    return voxelCoord;
}

vec3 SampleVelocityBilinear(vec3 voxelCoord, vec3 size)
{
    ivec3 floorVoxelCoord = ivec3(voxelCoord);
    int x = floorVoxelCoord.x;
    int y = floorVoxelCoord.y;
    int z = floorVoxelCoord.z;

    float fx = voxelCoord.x - x;
    float fy = voxelCoord.y - y;
    float fz = voxelCoord.z - z;

    int xp1 = int(min(size.x - 1, x + 1));
    int yp1 = int(min(size.y - 1, y + 1));
    int zp1 = int(min(size.z - 1, z + 1));

    vec3 x0 = GetVelocityDensityData(vec3(x, y, z)).xyz * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, y, z)).xyz * fx;
    vec3 x1 = GetVelocityDensityData(vec3(x, y, zp1)).xyz * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, y, zp1)).xyz  * fx;

    vec3 x2 = GetVelocityDensityData(vec3(x, yp1, z)).xyz * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, yp1, z)).xyz * fx;
    vec3 x3 = GetVelocityDensityData(vec3(x, yp1, zp1)).xyz * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, yp1, zp1)).xyz * fx;

    vec3 z0 = x0 * (1.0f - fz) + x1 * fz;
    vec3 z1 = x2 * (1.0f - fz) + x3 * fz;

    return z0 * (1.0f - fy) + z1 * fy;
}

float SampleBilinear(bool otherData, vec3 voxelCoord, vec3 size, int index)
{
    ivec3 floorVoxelCoord = ivec3(voxelCoord);
    int x = floorVoxelCoord.x;
    int y = floorVoxelCoord.y;
    int z = floorVoxelCoord.z;

    float fx = voxelCoord.x - x;
    float fy = voxelCoord.y - y;
    float fz = voxelCoord.z - z;

    int xp1 = int(min(size.x - 1, x + 1));
    int yp1 = int(min(size.y - 1, y + 1));
    int zp1 = int(min(size.z - 1, z + 1));

    float x0, x1, x2, x3;

    if (otherData == true)
    {
        x0 = GetPressureTempPhiReactionData(vec3(x, y, z))[index] * (1.0f - fx) + GetPressureTempPhiReactionData(vec3(xp1, y, z))[index] * fx;
        x1 = GetPressureTempPhiReactionData(vec3(x, y, zp1))[index] * (1.0f - fx) + GetPressureTempPhiReactionData(vec3(xp1, y, zp1))[index] * fx;

        x2 = GetPressureTempPhiReactionData(vec3(x, yp1, z))[index] * (1.0f - fx) + GetPressureTempPhiReactionData(vec3(xp1, yp1, z))[index] * fx;
        x3 = GetPressureTempPhiReactionData(vec3(x, yp1, zp1))[index] * (1.0f - fx) + GetPressureTempPhiReactionData(vec3(xp1, yp1, zp1))[index] * fx;
    } 
    else
    {
        x0 = GetVelocityDensityData(vec3(x, y, z))[index] * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, y, z))[index] * fx;
        x1 = GetVelocityDensityData(vec3(x, y, zp1))[index] * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, y, zp1))[index] * fx;

        x2 = GetVelocityDensityData(vec3(x, yp1, z))[index] * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, yp1, z))[index] * fx;
        x3 = GetVelocityDensityData(vec3(x, yp1, zp1))[index] * (1.0f - fx) + GetVelocityDensityData(vec3(xp1, yp1, zp1))[index] * fx;
    }

    float z0 = x0 * (1.0f - fz) + x1 * fz;
    float z1 = x2 * (1.0f - fz) + x3 * fz;

    return z0 * (1.0f - fy) + z1 * fy;
}

void AdvectDensityVelocity(vec3 voxelCoord, in vec4 curlObstaclesData, inout vec4 velocityDensityData)
{
    float obstacle = curlObstaclesData.w;
    if (obstacle > 0.1)
    {
        velocityDensityData = vec4(0.0);
        return;
    }

    vec3 advectedCoords = GetAdvectedPosCoords(voxelCoord, velocityDensityData);
    vec3 v = SampleVelocityBilinear(advectedCoords, m_size) * m_velocityDissipation;
    float d = SampleBilinear(false, advectedCoords, m_size, 3) * m_densityDissipation;

    velocityDensityData = vec4(v, d);
}

void AdvectReactionTemperature(vec3 voxelCoord, inout vec4 pressureTempPhiReactionData, in vec4 curlObstaclesData, in vec4 velocityDensityData)
{
    float obstacle = curlObstaclesData.w;
    if (obstacle > 0.1)
    {
        pressureTempPhiReactionData = vec4(pressureTempPhiReactionData.x, 0.0, pressureTempPhiReactionData.z, 0.0);
        return;
    }

    vec3 advectedCoords = GetAdvectedPosCoords(voxelCoord, velocityDensityData);
    float temp = SampleBilinear(true, advectedCoords, m_size, 1) * m_temperatureDissipation;
    float reaction = max(0, SampleBilinear(true, advectedCoords, m_size, 3) - m_reactionDecay);

    pressureTempPhiReactionData = vec4(pressureTempPhiReactionData.x, temp, pressureTempPhiReactionData.z, reaction);
}

void ApplyBuoyancy(vec3 voxelCoord, inout vec4 velocityDensityData, in vec4 pressureTempPhiReactionData)
{
    float T = pressureTempPhiReactionData.y;
    float D = velocityDensityData.w;
    vec3 V = velocityDensityData.xyz;

    if (T > m_ambientTemperature)
    {
        V += (dt * (T - m_ambientTemperature) * m_densityBuoyancy - D * m_densityWeight) * vec3(0, 1, 0);
    }

    velocityDensityData = vec4(V, D);
}

void ApplyImpulse(vec3 voxelCoord, float _Amount, int index, inout vec4 pressureTempPhiReactionData)
{
    vec3 pos = voxelCoord / m_size - m_inputPos;

    float mag = pos.x * pos.x + pos.y * pos.y + pos.z * pos.z;
    float rad2 = m_inputRadius * m_inputRadius;

    float amount = exp(-mag / rad2) * _Amount * dt;

    pressureTempPhiReactionData[index] += amount;
}

void ApplyExtinguishmentImpulse(vec3 voxelCoord, in vec4 pressureTempPhiReactionData, inout vec4 velocityDensityData)
{
    float amount = 0.0;
    float reaction = pressureTempPhiReactionData.w;

    if (reaction > 0.0 && reaction < m_reactionExtinguishment)
    {
        amount = m_densityAmount * reaction;
    }

    velocityDensityData.w += amount;
}

void ComputeVorticityConfinement(vec3 voxelCoord, inout vec4 curlObstaclesData, inout vec4 velocityDensityData)
{
    // vorticity
    vec3 idxL = vec3(max(0, voxelCoord.x - 1), voxelCoord.y, voxelCoord.z);
    vec3 idxR = vec3(min(m_size.x - 1, voxelCoord.x + 1), voxelCoord.y, voxelCoord.z);

    vec3 idxB = vec3(voxelCoord.x, max(0, voxelCoord.y - 1), voxelCoord.z);
    vec3 idxT = vec3(voxelCoord.x, min(m_size.y - 1, voxelCoord.y + 1), voxelCoord.z);
    
    vec3 idxD = vec3(voxelCoord.x, voxelCoord.y, max(0, voxelCoord.z - 1));
    vec3 idxU = vec3(voxelCoord.x, voxelCoord.y, min(m_size.z - 1, voxelCoord.z + 1));

    vec3 L = GetVelocityDensityData(idxL).xyz;
    vec3 R = GetVelocityDensityData(idxR).xyz;

    vec3 B = GetVelocityDensityData(idxB).xyz;
    vec3 T = GetVelocityDensityData(idxT).xyz;

    vec3 D = GetVelocityDensityData(idxD).xyz;
    vec3 U = GetVelocityDensityData(idxU).xyz;

    vec3 vorticity = 0.5 * vec3(((T.z - B.z) - (U.y - D.y)), ((U.x - D.x) - (R.z - L.z)), ((R.y - L.y) - (T.x - B.x)));

    float obstacle = curlObstaclesData.w;
    curlObstaclesData = vec4(vorticity, obstacle);

    // confinement
    float omegaL = length(GetCurlObstaclesData(idxL).xyz);
    float omegaR = length(GetCurlObstaclesData(idxR).xyz);

    float omegaB = length(GetCurlObstaclesData(idxB).xyz);
    float omegaT = length(GetCurlObstaclesData(idxT).xyz);

    float omegaD = length(GetCurlObstaclesData(idxD).xyz);
    float omegaU = length(GetCurlObstaclesData(idxU).xyz);

    vec3 omega = curlObstaclesData.xyz;

    vec3 eta = 0.5 * vec3(omegaR - omegaL, omegaT - omegaB, omegaU - omegaD);

    eta = normalize(eta + vec3(0.001, 0.001, 0.001));

    vec3 force = dt * m_vorticityStrength * vec3(eta.y * omega.z - eta.z * omega.y, eta.z * omega.x - eta.x * omega.z, eta.x * omega.y - eta.y * omega.x);

    velocityDensityData += vec4(force, 0.0);
}

void ComputeDivergence(vec3 voxelCoord, inout vec4 curlObstaclesData)
{
    vec3 idxL = vec3(max(0, voxelCoord.x - 1), voxelCoord.y, voxelCoord.z);
    vec3 idxR = vec3(min(m_size.x - 1, voxelCoord.x + 1), voxelCoord.y, voxelCoord.z);

    vec3 idxB = vec3(voxelCoord.x, max(0, voxelCoord.y - 1), voxelCoord.z);
    vec3 idxT = vec3(voxelCoord.x, min(m_size.y - 1, voxelCoord.y + 1), voxelCoord.z);

    vec3 idxD = vec3(voxelCoord.x, voxelCoord.y, max(0, voxelCoord.z - 1));
    vec3 idxU = vec3(voxelCoord.x, voxelCoord.y, min(m_size.z - 1, voxelCoord.z + 1));

    vec3 L = GetVelocityDensityData(idxL).xyz;
    vec3 R = GetVelocityDensityData(idxR).xyz;

    vec3 B = GetVelocityDensityData(idxB).xyz;
    vec3 T = GetVelocityDensityData(idxT).xyz;

    vec3 D = GetVelocityDensityData(idxD).xyz;
    vec3 U = GetVelocityDensityData(idxU).xyz;

    vec3 obstacleVelocity = vec3(0, 0, 0);

    if (GetCurlObstaclesData(idxL).w > 0.1)
    {
        L = obstacleVelocity;
    }
    if (GetCurlObstaclesData(idxR).w > 0.1)
    {
        R = obstacleVelocity;
    }

    if (GetCurlObstaclesData(idxB).w > 0.1)
    {
        B = obstacleVelocity;
    }
    if (GetCurlObstaclesData(idxT).w > 0.1)
    {
        T = obstacleVelocity;
    }

    if (GetCurlObstaclesData(idxD).w > 0.1)
    {
        D = obstacleVelocity;
    }
    if (GetCurlObstaclesData(idxU).w > 0.1)
    {
        U = obstacleVelocity;
    }

    float divergence = 0.5 * ((R.x - L.x) + (T.y - B.y) + (U.z - D.z));

    float obstacle = curlObstaclesData.w;
    curlObstaclesData = vec4(divergence, 0, 0, obstacle);
}

void ComputePressure(vec3 voxelCoord, in vec4 curlObstaclesData, inout vec4 pressureTempPhiReactionData)
{
    vec3 idxL = vec3(max(0, voxelCoord.x - 1), voxelCoord.y, voxelCoord.z);
    vec3 idxR = vec3(min(m_size.x - 1, voxelCoord.x + 1), voxelCoord.y, voxelCoord.z);

    vec3 idxB = vec3(voxelCoord.x, max(0, voxelCoord.y - 1), voxelCoord.z);
    vec3 idxT = vec3(voxelCoord.x, min(m_size.y - 1, voxelCoord.y + 1), voxelCoord.z);

    vec3 idxD = vec3(voxelCoord.x, voxelCoord.y, max(0, voxelCoord.z - 1));
    vec3 idxU = vec3(voxelCoord.x, voxelCoord.y, min(m_size.z - 1, voxelCoord.z + 1));

    float L = GetPressureTempPhiReactionData(idxL).x;
    float R = GetPressureTempPhiReactionData(idxR).x;

    float B = GetPressureTempPhiReactionData(idxB).x;
    float T = GetPressureTempPhiReactionData(idxT).x;

    float D = GetPressureTempPhiReactionData(idxD).x;
    float U = GetPressureTempPhiReactionData(idxU).x;

    float C = pressureTempPhiReactionData.x;

    float divergence = curlObstaclesData.x;

    if (GetCurlObstaclesData(idxL).w > 0.1)
    {
        L = C;
    }
    if (GetCurlObstaclesData(idxR).w > 0.1)
    {
        R = C;
    }

    if (GetCurlObstaclesData(idxB).w > 0.1)
    {
        B = C;
    }
    if (GetCurlObstaclesData(idxT).w > 0.1)
    {
        T = C;
    }

    if (GetCurlObstaclesData(idxD).w > 0.1)
    {
        D = C;
    }
    if (GetCurlObstaclesData(idxU).w > 0.1)
    {
        U = C;
    }

    pressureTempPhiReactionData.x = (L + R + B + T + U + D - divergence) / 6.0;
}

void ComputeProjection(vec3 voxelCoord, in vec4 curlObstaclesData, in vec4 pressureTempPhiReactionData, inout vec4 velocityDensityData)
{
    if (curlObstaclesData.w > 0.1)
    {
        velocityDensityData = vec4(vec3(0.0), velocityDensityData.w);
        return;
    }

    vec3 idxL = vec3(max(0, voxelCoord.x - 1), voxelCoord.y, voxelCoord.z);
    vec3 idxR = vec3(min(m_size.x - 1, voxelCoord.x + 1), voxelCoord.y, voxelCoord.z);

    vec3 idxB = vec3(voxelCoord.x, max(0, voxelCoord.y - 1), voxelCoord.z);
    vec3 idxT = vec3(voxelCoord.x, min(m_size.y - 1, voxelCoord.y + 1), voxelCoord.z);

    vec3 idxD = vec3(voxelCoord.x, voxelCoord.y, max(0, voxelCoord.z - 1));
    vec3 idxU = vec3(voxelCoord.x, voxelCoord.y, min(m_size.z - 1, voxelCoord.z + 1));

    float L = GetPressureTempPhiReactionData(idxL).x;
    float R = GetPressureTempPhiReactionData(idxR).x;

    float B = GetPressureTempPhiReactionData(idxB).x;
    float T = GetPressureTempPhiReactionData(idxT).x;

    float D = GetPressureTempPhiReactionData(idxD).x;
    float U = GetPressureTempPhiReactionData(idxU).x;

    float C = pressureTempPhiReactionData.x;

    vec3 mask = vec3(1.0);

    if (GetCurlObstaclesData(idxL).w > 0.1)
    {
        L = C;
        mask.x = 0;
    }
    if (GetCurlObstaclesData(idxR).w > 0.1)
    {
        R = C;
        mask.x = 0;
    }

    if (GetCurlObstaclesData(idxB).w > 0.1)
    {
        B = C;
        mask.y = 0;
    }
    if (GetCurlObstaclesData(idxT).w > 0.1)
    {
        T = C;
        mask.y = 0;
    }

    if (GetCurlObstaclesData(idxD).w > 0.1)
    {
        D = C;
        mask.z = 0;
    }
    if (GetCurlObstaclesData(idxU).w > 0.1)
    {
        U = C;
        mask.z = 0;
    }

    vec3 v = velocityDensityData.xyz - vec3(R - L, T - B, U - D) * 0.5;
    velocityDensityData = vec4(v * mask, velocityDensityData.w);
}

void main()
{
    ivec3 voxelCoord = ivec3(gl_GlobalInvocationID);

    vec4 velocityDensityData = GetVelocityDensityData(voxelCoord);
    vec4 pressureTempPhiReactionData = GetPressureTempPhiReactionData(voxelCoord);
    vec4 curlObstaclesData = GetCurlObstaclesData(voxelCoord);

    if (iTime <= 1.0)
    {
        ComputeObstacles(voxelCoord, curlObstaclesData);
    }

    AdvectDensityVelocity(voxelCoord, curlObstaclesData, velocityDensityData);
    AdvectReactionTemperature(voxelCoord, pressureTempPhiReactionData, curlObstaclesData, velocityDensityData);
    ApplyBuoyancy(voxelCoord, velocityDensityData, pressureTempPhiReactionData);
    ApplyImpulse(voxelCoord, m_reactionAmount, 3, pressureTempPhiReactionData);
    ApplyImpulse(voxelCoord, m_temperatureAmount, 1, pressureTempPhiReactionData);
    ApplyExtinguishmentImpulse(voxelCoord, pressureTempPhiReactionData, velocityDensityData);
    ComputeVorticityConfinement(voxelCoord, curlObstaclesData, velocityDensityData);
    ComputeDivergence(voxelCoord, curlObstaclesData);
    for (int i = 0; i < m_iterations; i++)
    {
        ComputePressure(voxelCoord, curlObstaclesData, pressureTempPhiReactionData);
    }
    ComputeProjection(voxelCoord, curlObstaclesData, pressureTempPhiReactionData, velocityDensityData);

    imageStore(velocityDensityTexture, ivec3(voxelCoord), velocityDensityData);
    imageStore(pressureTempPhiReactionTexture, ivec3(voxelCoord), pressureTempPhiReactionData);
    imageStore(curlObstaclesTexture, ivec3(voxelCoord), curlObstaclesData);
}