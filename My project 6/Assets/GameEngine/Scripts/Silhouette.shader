// [수정] 이제 이 셰이더가 플레이어의 기본 셰이더가 됩니다.
Shader "Unlit/PlayerAlwaysVisible"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // 플레이어가 항상 벽보다 앞에 그려지도록 렌더링 큐를 조정합니다.
        Tags { "Queue"="Transparent+1" }
        LOD 100

        Pass
        {
            // 이 부분이 핵심입니다.
            // 깊이 테스트를 항상 통과시켜서, 벽 뒤에 있어도 무조건 그려지게 합니다.
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 텍스처에서 색상을 가져옵니다.
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
