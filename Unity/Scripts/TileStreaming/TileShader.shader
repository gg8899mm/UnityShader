Shader "Terrain/KTX2Tile"
{
    Properties
    {
        _MainTex ("Tile Texture", 2D) = "gray" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Tint ("Color Tint", Color) = (1, 1, 1, 1)
        _TileX ("Tile X", Float) = -1
        _TileY ("Tile Y", Float) = -1
        _TileZ ("Tile Z", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _MainTex;
            sampler2D _NormalMap;
            float4 _MainTex_ST;
            float4 _NormalMap_ST;
            float _Metallic;
            float _Smoothness;
            float4 _Tint;
            float _TileX;
            float _TileY;
            float _TileZ;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldTangent : TEXCOORD2;
                float3 worldBitangent : TEXCOORD3;
                float3 worldPos : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * v.tangent.w;
                
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 baseColor = tex2D(_MainTex, i.uv).rgb * _Tint.rgb;
                
                float3 normal = UnpackNormal(tex2D(_NormalMap, i.uv));
                float3x3 tbn = float3x3(i.worldTangent, i.worldBitangent, i.worldNormal);
                float3 worldNormal = normalize(mul(normal, tbn));

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                
                float ndotl = max(dot(worldNormal, lightDir), 0.0);
                float3 diffuse = baseColor * _LightColor0.rgb * ndotl;
                
                float3 halfDir = normalize(lightDir + viewDir);
                float ndoth = max(dot(worldNormal, halfDir), 0.0);
                float specPower = exp2(_Smoothness * 10.0 + 1.0);
                float3 specular = pow(ndoth, specPower) * _Metallic * _LightColor0.rgb;
                
                float3 ambient = baseColor * UNITY_LIGHTMODEL_AMBIENT.rgb;
                float3 finalColor = ambient + diffuse + specular;
                
                return float4(finalColor, 1.0);
            }

            ENDCG
        }
    }

    Fallback "Diffuse"
}
