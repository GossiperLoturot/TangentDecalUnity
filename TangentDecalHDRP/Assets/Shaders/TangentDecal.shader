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
            fixed4 decalAlbedo = tex2D(_DecalAlbedoTex, d.uv);
            fixed4 decalMask = tex2D(_DecalMaskTex, d.uv);

            fixed4 col;
            col.r = lerp(baseAlbedo.r, decalAlbedo.r, d.mask * decalAlbedo.a);
            col.g = lerp(baseAlbedo.g, decalAlbedo.g, d.mask * decalAlbedo.a);
            col.b = lerp(baseAlbedo.b, decalAlbedo.b, d.mask * decalAlbedo.a);
            col.a = lerp(baseAlbedo.a, decalMask.b, d.mask * decalMask.a);

            return col;
        }

        fixed4 fragNormal(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            fixed4 baseNormal = tex2D(_NormalTex, i.uv);
            fixed4 decalNormal = tex2D(_DecalNormalTex, d.uv);

            fixed4 normal;
            #if defined(UNITY_NO_DXT5nm)
                normal.r = lerp(baseNormal.r, decalNormal.r, d.mask * decalNormal.a);
                normal.g = lerp(baseNormal.g, decalNormal.g, d.mask * decalNormal.a);
                normal.b = lerp(baseNormal.b, decalNormal.b, d.mask * decalNormal.a);
                normal.a = baseNormal.a;
            #else // DXT5nm or BC5
                normal.a = lerp(baseNormal.a, decalNormal.r, d.mask * decalNormal.a);
                normal.g = lerp(baseNormal.g, decalNormal.g, d.mask * decalNormal.a);
                normal.b = lerp(baseNormal.b, decalNormal.b, d.mask * decalNormal.a);
                normal.r = baseNormal.r;
            #endif

            return normal;
        }

        fixed4 fragMask(v2f i) : SV_Target
        {
            decalOut d = computeDecal(i);

            fixed4 baseMask = tex2D(_MaskTex, i.uv);
            fixed4 decalMask = tex2D(_DecalMaskTex, d.uv);

            fixed4 mask;
            mask.a = lerp(baseMask.a, decalMask.r, d.mask * decalMask.a);
            mask.r = lerp(baseMask.r, decalMask.g, d.mask * decalMask.a);
            mask.g = baseMask.g;
            mask.b = baseMask.b;

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
