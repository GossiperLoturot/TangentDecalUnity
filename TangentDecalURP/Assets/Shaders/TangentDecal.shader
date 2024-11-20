Shader "Hidden/TangentDecal"
{
    SubShader
    {
        Cull Off

        HLSLINCLUDE
        #include "UnityCG.cginc"

        sampler2D _AlbedoTex;
        sampler2D _NormalTex;
        sampler2D _MaskTex;
        sampler2D _AlphaTex;
        sampler2D _DecalAlbedoTex;
        sampler2D _DecalNormalTex;
        sampler2D _DecalMaskTex;
        sampler2D _DecalAlphaTex;
        half3 _DecalPosition;
        half3 _DecalNormal;
        half3 _DecalTangent;
        float3 _DecalScale;

        struct appdata
        {
            float4 vertex : POSITION;
            half3 normal : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 vertexWorld : TEXCOORD1;
            half3 normalWorld : TEXCOORD2;
        };

        struct decalOut
        {
            float2 uv;
            fixed mask;
        };

        v2f vert(appdata v)
        {
            v2f o;

            float x = v.uv.x * 2.0 - 1.0;
            float y = (v.uv.y * 2.0 - 1.0) * _ProjectionParams.x;
            o.vertex = float4(x, y, 0.0, 1.0);

            o.uv = v.uv;
            o.vertexWorld = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.normalWorld = UnityObjectToWorldNormal(v.normal);

            return o;
        }

        decalOut computeDecal(v2f i)
        {
            decalOut o;

            half3 normal = normalize(_DecalNormal);
            half3 tangent = normalize(_DecalTangent);
            half3 bitangent = normalize(cross(normal, tangent));

            tangent = normalize(cross(normal, bitangent));

            float3 p = i.vertexWorld - _DecalPosition;
            float3 prj = float3(dot(tangent, p), dot(bitangent, p), dot(normal, p)) / _DecalScale + 0.5;

            o.uv = prj.xy;
            o.mask = int(0.0 <= prj.x && prj.x <= 1.0 && 0.0 <= prj.y && prj.y <= 1.0 && 0.0 <= prj.z && prj.z <= 1.0);

            return o;
        }

        fixed4 fragAlbedo(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            fixed4 baseAlbedo = tex2D(_AlbedoTex, i.uv);
            fixed4 decalAlbedo = tex2D(_DecalAlbedoTex, d.uv) * d.mask;

            // A over B alpha blending
            fixed4 albedo;
            albedo.a = lerp(baseAlbedo.a, 1.0, decalAlbedo.a);
            if (albedo.a != 0.0)
            {
                albedo.rgb = lerp(baseAlbedo.a * baseAlbedo.rgb, decalAlbedo.rgb, decalAlbedo.a) / albedo.a;
            }
            return albedo;
        }

        fixed4 fragNormal(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            fixed4 baseNormal = tex2D(_NormalTex, i.uv);
            fixed4 decalNormal = tex2D(_DecalNormalTex, d.uv) * d.mask;

            // A over B alpha blending
            fixed4 normal;
            normal.a = lerp(baseNormal.a, 1.0, decalNormal.a);
            if (normal.a != 0.0)
            {
                normal.rgb = lerp(baseNormal.a * baseNormal.rgb, decalNormal.rgb, decalNormal.a) / normal.a;
            }
            return normal;
        }

        fixed4 fragMask(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            // (A) smoothness, (R) metalic, (G) ambient occulusion, (B) alpha
            fixed4 baseMask = tex2D(_MaskTex, i.uv);
            fixed4 decalMask = tex2D(_DecalMaskTex, d.uv) * d.mask;

            // A over B alpha blending
            fixed4 mask;
            mask.b = lerp(baseMask.b, 1.0, decalMask.b);
            if (mask.b != 0.0)
            {
                mask.arg = lerp(baseMask.b * baseMask.arg, decalMask.arg, decalMask.b) / mask.b;
            }
            return mask;
        }

        fixed4 fragAlpha(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            fixed4 baseAlpha = tex2D(_AlphaTex, i.uv);
            fixed4 decalAlpha = tex2D(_DecalAlphaTex, d.uv) * d.mask;

            // A over B alpha blending
            fixed4 alpha;
            alpha.a = lerp(baseAlpha.a, 1.0, decalAlpha.a);
            if (alpha.a != 0.0)
            {
                alpha.rgb = lerp(baseAlpha.a * baseAlpha.rgb, decalAlpha.rgb, decalAlpha.a) / alpha.a;
            }
            return alpha;
        }
        ENDHLSL

        Pass
        {
            Name "DecalAlbedoPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragAlbedo
            ENDHLSL
        }

        Pass
        {
            Name "DecalNormalPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragNormal
            ENDHLSL
        }

        Pass
        {
            Name "DecalMaskPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragMask
            ENDHLSL
        }

        Pass
        {
            Name "DecalAlphaPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragAlpha
            ENDHLSL
        }
    }
}
