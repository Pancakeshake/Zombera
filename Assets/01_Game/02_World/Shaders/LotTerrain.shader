Shader "Zombera/LotTerrain"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor]  _BaseColor("Color", Color) = (1,1,1,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _TilingScale("Tiling", Float) = 1.0
        _AntiTileStrength("Anti-Tile Strength", Range(0, 1)) = 0.35
        _AntiTileScale("Anti-Tile Scale", Range(1, 32)) = 12
        _NormalNoiseStrength("Normal Noise Strength", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2;
                float2 uv         : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);       SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);       SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _BumpScale;
                half   _Smoothness;
                half   _TilingScale;
                half   _AntiTileStrength;
                half   _AntiTileScale;
                half   _NormalNoiseStrength;
            CBUFFER_END

            // ── Noise ───────────────────────────────────────────────

            half NoiseHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                half a = NoiseHash(i);
                half b = NoiseHash(i + float2(1, 0));
                half c = NoiseHash(i + float2(0, 1));
                half d = NoiseHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half FbmNoise(float2 p, float scale)
            {
                half n = 0;
                half amp = 1.0;
                half freq = 1.0;
                for (int k = 0; k < 3; k++)
                {
                    n += ValueNoise(p * scale * freq) * amp;
                    freq *= 2.17;
                    amp *= 0.5;
                }
                return n;
            }

            // ── Vertex ──────────────────────────────────────────────

            Varyings LitPassVertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                float4 tangentWS = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
                OUT.tangentWS = tangentWS;

                OUT.uv = IN.uv;
                return OUT;
            }

            // ── Fragment ────────────────────────────────────────────

            half4 LitPassFragment(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv * _TilingScale;

                // ── Albedo + albedo anti-tile ────────────────────────
                half4 albedo = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, ddx(uv), ddy(uv));
                half noise = FbmNoise(IN.uv, _AntiTileScale);
                half variation = lerp(0.93, 1.07, noise);
                albedo.rgb *= lerp(1.0, variation, _AntiTileStrength);
                albedo *= _BaseColor;

                // ── Normal + normal anti-tile ────────────────────────
                float3 normalWS = normalize(IN.normalWS);
                float3 tangentWS = normalize(IN.tangentWS.xyz);
                float3 bitangentWS = cross(normalWS, tangentWS) * IN.tangentWS.w;

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(_BumpMap, sampler_BumpMap, uv, ddx(uv), ddy(uv)));
                normalTS.xy *= _BumpScale;

                // Normal anti-tile: blend FBM noise into the tangent-space normal.
                half normalNoise = FbmNoise(IN.uv + 0.37, _AntiTileScale * 0.5);
                half2 normalJitter = (half2(normalNoise, FbmNoise(IN.uv + 0.73, _AntiTileScale * 0.5)) - 0.5) * 2.0;
                normalTS.xy = lerp(normalTS.xy, normalTS.xy + normalJitter * 0.15, _NormalNoiseStrength);
                normalTS = normalize(normalTS);

                float3x3 TBN = float3x3(tangentWS, bitangentWS, normalWS);
                normalWS = normalize(mul(normalTS, TBN));

                // ── Lighting ─────────────────────────────────────────
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = albedo.rgb * mainLight.color * NdotL;
                half3 ambient = SampleSH(normalWS) * albedo.rgb * 0.5;

                // Specular (Blinn-Phong).
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                half spec = pow(max(dot(normalWS, halfDir), 0.0), exp2(_Smoothness * 10.0 + 1.0));
                half3 specular = mainLight.color * spec * _Smoothness;

                half3 color = diffuse + ambient + specular;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
