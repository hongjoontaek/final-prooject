// Silhouette.shader
Shader "Unlit/Silhouette"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Offset -1, -1                  // 렌더링 순서를 앞으로 당겨서 벽보다 앞에 보이게 함
            Blend SrcAlpha OneMinusSrcAlpha // 알파 블렌딩 활성화
            ZWrite Off                     // 깊이 버퍼에 쓰지 않음
            ZTest Always                   // 깊이 테스트를 항상 통과 (다른 물체 뒤에 있어도 그려짐)
            Cull Off                       // 뒷면도 렌더링 (선택 사항)

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
    FallBack "Transparent/VertexLit"
}
