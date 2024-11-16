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

        fixed4 frag(v2f i) : SV_Target
        {
            half3 normal = normalize(_DecalNormal);
            half3 tangent = normalize(_DecalTangent);
            half3 bitangent = normalize(cross(normal, tangent));

            tangent = normalize(cross(normal, bitangent));

            float3 p = i.vertexWorld - _DecalPosition;
            float3 prj = float3(dot(tangent, p), dot(bitangent, p), dot(normal, p)) / _DecalScale + 0.5;

            fixed mask = int(0.0 <= prj.x && prj.x <= 1.0 && 0.0 <= prj.y && prj.y <= 1.0 && 0.0 <= prj.z && prj.z <= 1.0);
            float2 uv = prj.xy;

            fixed4 baseCol = tex2D(_MainTex, i.uv);
            fixed4 decalCol = tex2D(_DecalTex, uv);
            fixed4 col = lerp(baseCol, decalCol, mask * decalCol.a);

            return col;
        }
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
