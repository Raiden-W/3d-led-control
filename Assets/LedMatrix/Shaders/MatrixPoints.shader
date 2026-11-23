Shader "LedMatrix/Points"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Atlas); SAMPLER(sampler_Atlas);
            float4x4 _MatrixToWorld;
            float4 _PointCounts, _Spacing, _AtlasGrid;
            float _PointSize;
            struct Out { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 atlas:TEXCOORD1; };
            Out Vert(uint id:SV_VertexID)
            {
                uint nx=(uint)_PointCounts.x, ny=(uint)_PointCounts.y;
                uint index=id/6, x=index%nx, y=(index/nx)%ny, z=index/(nx*ny);
                float2 corners[6]={float2(-1,-1),float2(-1,1),float2(1,1),float2(-1,-1),float2(1,1),float2(1,-1)};
                float2 q=corners[id%6];
                float3 p=mul(_MatrixToWorld,float4(((float)x-(_PointCounts.x-1)*0.5)*_Spacing.x,y*_Spacing.y,((float)z-(_PointCounts.z-1)*0.5)*_Spacing.z,1)).xyz;
                p+=UNITY_MATRIX_I_V._m00_m10_m20*q.x*_PointSize+UNITY_MATRIX_I_V._m01_m11_m21*q.y*_PointSize;
                Out o; o.pos=TransformWorldToHClip(p); o.uv=q;
                uint cols=(uint)_AtlasGrid.x;
                o.atlas=(float2(z%cols,_AtlasGrid.y-1-z/cols)+float2((x+0.5)/_PointCounts.x,(y+0.5)/_PointCounts.y))/_AtlasGrid.xy;
                return o;
            }
            half4 Frag(Out i):SV_Target
            {
                clip(1-dot(i.uv,i.uv));
                float3 c=SAMPLE_TEXTURE2D(_Atlas,sampler_Atlas,i.atlas).rgb;
                c=lerp(c/12.92,pow((c+0.055)/1.055,2.4),step(0.04045,c));
                return float4(max(c,0.008),1);
            }
            ENDHLSL
        }
    }
}
