// Moonlit Tails toon shader (URP, Quest friendly).
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
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor; half4 _ShadeColor; half _ShadeThreshold; half _ShadeSoftness;
            half _MinBrightness; float4 _KeyDir; half4 _KeyColor; half _SceneInfluence;
            half4 _RimColor; half _RimPower; half _RimStrength; half4 _EmissionColor;
            half4 _OutlineColor; float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; half fog : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Cel(half ndl) { return smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndl); }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                // virtual key light: fixed relative to the camera, so the face side you look at is always lit
                float3 keyDir = normalize(mul((float3x3)UNITY_MATRIX_I_V, _KeyDir.xyz));
                half3 virtualLit = lerp(_ShadeColor.rgb, half3(1,1,1), Cel(dot(n, keyDir))) * _KeyColor.rgb;

                // real scene main light + ambient, also cel-stepped
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half sceneRamp = Cel(dot(n, ml.direction) * ml.shadowAttenuation);
                half3 sceneLit = lerp(_ShadeColor.rgb * 0.5, half3(1,1,1), sceneRamp) * ml.color + SampleSH(n);

                half3 light = lerp(virtualLit, sceneLit, _SceneInfluence);
                light = max(light, _MinBrightness.xxx);

                half rim = pow(1 - saturate(dot(n, v)), _RimPower) * _RimStrength;
                half3 col = albedo.rgb * light + rim * _RimColor.rgb + _EmissionColor.rgb;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz) + TransformObjectToWorldNormal(v.normalOS) * _OutlineWidth;
                o.positionCS = TransformWorldToHClip(posWS);
                return o;
            }
            half4 frag (Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
