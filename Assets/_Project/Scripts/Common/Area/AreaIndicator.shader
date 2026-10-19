// Depth-projected area indicator (circle / ring / cone / line). Draws a box volume, reconstructs the world position of whatever
// geometry is behind each pixel from the camera depth texture, and lights it up if that position falls inside the shape. Because
// it works from real surface positions, it follows stairs, slopes and uneven ground within [-HeightBelow, +HeightAbove] of the
// indicator origin. Requires "Depth Texture" enabled on the URP asset. Shape maths mirrors AreaShape.SignedDistance in C#.
Shader "Game/AreaIndicator"
{
    Properties
    {
        _FillColor ("Fill Color", Color) = (0.3, 0.9, 1, 0.15)
        _EdgeColor ("Edge Color", Color) = (0.3, 0.9, 1, 0.9)
        _Shape ("Shape (0 circle, 1 cone, 2 line)", Float) = 0
        _Range ("Range / Length", Float) = 3
        _InnerRadius ("Inner Radius (ring)", Float) = 0
        _HalfAngle ("Half Angle (radians, cone)", Float) = 0.7854
        _Width ("Width (line)", Float) = 1
        _EdgeWidth ("Edge Width", Float) = 0.08
        _HeightAbove ("Height Above Origin", Float) = 1.5
        _HeightBelow ("Height Below Origin", Float) = 1.5
        _HeightFade ("Height Fade", Float) = 0.3
        _DashLength ("Edge Dash Length (0 = solid)", Float) = 0
        _DashGap ("Edge Dash Gap", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "AreaIndicator"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            half4 _FillColor;
            half4 _EdgeColor;
            float _Shape;
            float _Range;
            float _InnerRadius;
            float _HalfAngle;
            float _Width;
            float _EdgeWidth;
            float _HeightAbove;
            float _HeightBelow;
            float _HeightFade;
            float _DashLength;
            float _DashGap;
            float4x4 _AreaWorldToLocal; // rotation + translation only (no scale); set by AreaIndicator

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            // Negative inside, positive outside. p.x = right, p.y = forward (ground plane).
            float ShapeDistance(float2 p)
            {
                if (_Shape < 0.5)
                {
                    float d = length(p);
                    float sd = d - _Range;
                    return _InnerRadius > 0.0 ? max(sd, _InnerRadius - d) : sd;
                }

                if (_Shape < 1.5)
                {
                    float wedge = abs(p.x) * cos(_HalfAngle) - p.y * sin(_HalfAngle);
                    return max(length(p) - _Range, wedge);
                }

                float2 q = abs(float2(p.x, p.y - _Range * 0.5)) - float2(_Width * 0.5, _Range * 0.5);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
            }

            // Distance travelled along the shape's border, in world units, used to lay out dashes.
            float EdgeCoord(float2 p)
            {
                float d = length(p);

                if (_Shape < 0.5)
                {
                    return atan2(p.x, p.y) * d; // polar arc length; the seam sits behind the origin
                }

                if (_Shape < 1.5)
                {
                    float arc = d - _Range;
                    float wedge = abs(p.x) * cos(_HalfAngle) - p.y * sin(_HalfAngle);
                    return arc > wedge ? atan2(p.x, p.y) * d : d; // curved end vs straight sides
                }

                float2 q = abs(float2(p.x, p.y - _Range * 0.5)) - float2(_Width * 0.5, _Range * 0.5);
                return q.x > q.y ? p.y : p.x; // long sides run along y, short ends along x
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.00001) discard; // sky / beyond the far plane
                #else
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                    if (rawDepth >= 0.99999) discard;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 local = mul(_AreaWorldToLocal, float4(positionWS, 1.0)).xyz;

                // Vertical window around the origin, with a soft fade near its limits.
                float h = local.y;
                float limit = h >= 0.0 ? _HeightAbove : _HeightBelow;
                float fade = max(min(_HeightFade, limit), 0.0001);
                float heightMask = 1.0 - smoothstep(limit - fade, limit, abs(h));
                if (heightMask <= 0.001) discard;

                float sd = ShapeDistance(local.xz);
                float aa = max(fwidth(sd), 0.0001);

                float inside = saturate(0.5 - sd / aa);
                float interior = saturate(0.5 - (sd + _EdgeWidth) / aa);
                float edge = saturate(inside - interior);

                if (_DashLength > 0.0 && _DashGap > 0.0)
                {
                    float u = EdgeCoord(local.xz);
                    float period = _DashLength + _DashGap;
                    float f = frac(u / period) * period;
                    float aaU = max(fwidth(u), 0.0001);
                    edge *= min(saturate(f / aaU), saturate((_DashLength - f) / aaU));
                }

                float fillA = _FillColor.a * interior;
                float edgeA = _EdgeColor.a * edge;
                float alpha = fillA + edgeA;
                if (alpha <= 0.002) discard;

                half3 rgb = (_FillColor.rgb * fillA + _EdgeColor.rgb * edgeA) / alpha;
                return half4(rgb, alpha * heightMask);
            }
            ENDHLSL
        }
    }
}
