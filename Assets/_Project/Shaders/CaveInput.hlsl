#ifndef GAME_CAVE_INPUT_INCLUDED
#define GAME_CAVE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

// Same property layout in every pass so the SRP Batcher stays happy.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4 _BaseColor;
    half4 _SpecColor;
    half4 _EmissionColor;
    half _Cutoff;
    float _TileSize;
    float _BlendSharpness;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// World-space box projection: texture repeats every _TileSize world units no matter how
// the mesh is scaled, so a 13x3 wall and a 13x13 floor get the same texel density.
half3 SampleTriplanar(float3 positionWS, float3 normalWS)
{
    float3 w = pow(abs(normalWS), _BlendSharpness);
    w /= max(w.x + w.y + w.z, 1e-5);

    float inv = 1.0 / max(_TileSize, 1e-3);
    half3 x = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.zy * inv).rgb;
    half3 y = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xz * inv).rgb;
    half3 z = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xy * inv).rgb;
    return x * w.x + y * w.y + z * w.z;
}

#endif
