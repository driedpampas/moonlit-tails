// Moonlit Tails toon shader (URP, Quest & PCVR Single-Pass Instanced friendly).
// Cel-shaded with a built-in "virtual key light" so characters are ALWAYS readable,
// no matter how dark the bar lighting is. Blend toward real scene lights with _SceneInfluence.
// Single main light only (no per-pixel additional-light loop) to stay cheap on Quest 2.
Shader "MoonlitBar/Toon"
{
    Properties
    {
        _BaseMap ("Base Map (atlas)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Header(Cel Shading)]
        _ShadeColor ("Shadow Tint", Color) = (0.72, 0.66, 0.86, 1)
        _ShadeThreshold ("Shadow Threshold", Range(-1,1)) = 0.1
        _ShadeSoftness ("Shadow Edge Softness", Range(0.001,0.5)) = 0.04
        _MinBrightness ("Minimum Brightness", Range(0,1)) = 0.55
        [Header(Virtual Key Light)]
        _KeyDir ("Key Light Direction (view space)", Vector) = (0.4, 0.6, 0.7, 0)
        _KeyColor ("Key Light Color", Color) = (1, 0.96, 0.9, 1)
        _SceneInfluence ("Scene Light Influence", Range(0,1)) = 0.35
        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.92, 0.8, 1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimStrength ("Rim Strength", Range(0,1)) = 0.35
        [Header(Emission)]
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.13, 0.12, 0.22, 1)
        _OutlineWidth ("Outline Width (m)", Range(0, 0.01)) = 0.0025
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" }

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half4 _ShadeColor; half _ShadeThreshold; half _ShadeSoftness;
                half _MinBrightness; float4 _KeyDir; half4 _KeyColor; half _SceneInfluence;
                half4 _RimColor; half _RimPower; half _RimStrength; half4 _EmissionColor;
                half4 _OutlineColor; float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half3 vertexSH : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.vertexSH = max(SampleSH(o.normalWS), 0.0h);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Cel(half ndl)
            {
                half s = max(_ShadeSoftness, 0.001h);
                return smoothstep(_ShadeThreshold - s, _ShadeThreshold + s, ndl);
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = SafeNormalize(i.normalWS);
                float3 v = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

                // virtual key light built from view direction + world up (no inverse-view matrix dependency in XR stereo)
                float3 upRef = abs(v.y) > 0.99 ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 camRight = SafeNormalize(cross(upRef, v));
                float3 camUp = cross(v, camRight);
                float3 keyDir = SafeNormalize(camRight * _KeyDir.x + camUp * _KeyDir.y + v * _KeyDir.z);
                half3 virtualLit = lerp(_ShadeColor.rgb, half3(1, 1, 1), Cel(dot(n, keyDir))) * _KeyColor.rgb;

                // real scene main light + ambient, also cel-stepped
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half sceneRamp = Cel(dot(n, ml.direction) * ml.shadowAttenuation);
                half3 sceneLit = lerp(_ShadeColor.rgb * 0.5h, half3(1, 1, 1), sceneRamp) * ml.color + i.vertexSH;

                half3 light = lerp(virtualLit, sceneLit, _SceneInfluence);
                light = max(light, _MinBrightness.xxx);

                half ndv = saturate(dot(n, v));
                half rim = pow(max(1.0h - ndv, 1e-4h), _RimPower) * _RimStrength;
                half3 col = max(albedo.rgb * light + rim * _RimColor.rgb + _EmissionColor.rgb, 0.0h);
                col = MixFog(col, i.fog);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half4 _ShadeColor; half _ShadeThreshold; half _ShadeSoftness;
                half _MinBrightness; float4 _KeyDir; half4 _KeyColor; half _SceneInfluence;
                half4 _RimColor; half _RimPower; half _RimStrength; half4 _EmissionColor;
                half4 _OutlineColor; float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz) + SafeNormalize(TransformObjectToWorldNormal(v.normalOS)) * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(posWS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(_OutlineColor.rgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half4 _ShadeColor; half _ShadeThreshold; half _ShadeSoftness;
                half _MinBrightness; float4 _KeyDir; half4 _KeyColor; half _SceneInfluence;
                half4 _RimColor; half _RimPower; half _RimStrength; half4 _EmissionColor;
                half4 _OutlineColor; float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings i) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
