Shader "Hidden/TangentDecal"
{
    SubShader
    {
        Cull Off

        HLSLINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _DecalTex;
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
            decalOut m = computeDecal(i);

            fixed4 base = tex2D(_MainTex, i.uv);
            fixed4 decal = tex2D(_DecalTex, m.uv) * m.mask;

            // A over B alpha blending
            fixed4 o;
            o.a = lerp(base.a, 1.0, decal.a);
            if (o.a != 0.0)
            {
                o.rgb = lerp(base.a * base.rgb, decal.rgb, decal.a) / o.a;
            }
            return o;
        }

        fixed4 fragNormal(v2f i) : SV_Target
        {
            decalOut m = computeDecal(i);

            fixed4 base = tex2D(_MainTex, i.uv);
            fixed4 decal = tex2D(_DecalTex, m.uv) * m.mask;

            // A over B alpha blending
            fixed4 o;
            o.a = lerp(base.a, 1.0, decal.a);
            if (o.a != 0.0)
            {
                o.rgb = lerp(base.a * base.rgb, decal.rgb, decal.a) / o.a;
            }
            return o;
        }

        fixed4 fragMask(v2f i) : SV_Target
        {
            decalOut m = computeDecal(i);

            // (A) smoothness, (R) metalic, (G) ambient occulusion, (B) alpha
            fixed4 base = tex2D(_MainTex, i.uv);
            fixed4 decal = tex2D(_DecalTex, m.uv) * m.mask;

            // A over B alpha blending
            fixed4 o;
            o.b = lerp(base.b, 1.0, decal.b);
            if (o.b != 0.0)
            {
                o.rga = lerp(base.b * base.rga, decal.rga, decal.b) / o.b;
            }
            return o;
        }

        fixed4 fragAlpha(v2f i) : SV_Target
        {
            decalOut m = computeDecal(i);

            fixed4 base = tex2D(_MainTex, i.uv);
            fixed4 decal = tex2D(_DecalTex, m.uv) * m.mask;

            // A over B alpha blending
            fixed4 o;
            o.a = lerp(base.a, 1.0, decal.a);
            if (o.a != 0.0)
            {
                o.rgb = lerp(base.a * base.rgb, decal.rgb, decal.a) / o.a;
            }
            return o;
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
