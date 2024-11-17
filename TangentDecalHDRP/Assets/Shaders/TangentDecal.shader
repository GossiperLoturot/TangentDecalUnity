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
        sampler2D _DecalAlbedoTex;
        sampler2D _DecalNormalTex;
        sampler2D _DecalMaskTex;
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

            fixed4 albedo;
            albedo.r = lerp(baseAlbedo.r, 1.0, decalAlbedo.r * decalAlbedo.a);
            albedo.g = lerp(baseAlbedo.g, 1.0, decalAlbedo.g * decalAlbedo.a);
            albedo.b = lerp(baseAlbedo.b, 1.0, decalAlbedo.b * decalAlbedo.a);
            albedo.a = lerp(baseAlbedo.a, 1.0, decalAlbedo.a);
            return albedo;
        }

        fixed4 fragNormal(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            fixed4 baseNormal = tex2D(_NormalTex, i.uv);
            fixed4 decalNormal = tex2D(_DecalNormalTex, d.uv) * d.mask;

            fixed4 normal;
            normal.r = lerp(baseNormal.r, 1.0, decalNormal.r * decalNormal.a);
            normal.g = lerp(baseNormal.g, 1.0, decalNormal.g * decalNormal.a);
            normal.b = lerp(baseNormal.b, 1.0, decalNormal.b * decalNormal.a);
            normal.a = lerp(baseNormal.a, 1.0, decalNormal.a);
            return normal;
        }

        fixed4 fragMask(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            fixed4 baseMask = tex2D(_MaskTex, i.uv);
            fixed4 decalMask = tex2D(_DecalMaskTex, d.uv) * d.mask;

            fixed4 mask;
            mask.r = lerp(baseMask.r, 1.0, decalMask.r * decalMask.b);
            mask.g = lerp(baseMask.g, 1.0, decalMask.g * decalMask.b);
            mask.b = lerp(baseMask.b, 1.0, decalMask.b);
            mask.a = lerp(baseMask.a, 1.0, decalMask.a * decalMask.b);
            return mask;
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
    }
}
