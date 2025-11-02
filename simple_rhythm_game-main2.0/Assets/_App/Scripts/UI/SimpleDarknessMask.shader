Shader "Custom/SimpleDarknessMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CenterX ("Center X", Range(0,1)) = 0.5
        _CenterY ("Center Y", Range(0,1)) = 0.3
        _Radius ("Radius", Range(0,1)) = 0.15
        _Progress ("Progress", Range(0,1)) = 0
        _Color ("Tint", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+1000"  // 提高渲染队列确保在最前面
            "IgnoreProjector"="True" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ PIXELSNAP_ON
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            
            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _CenterX;
            float _CenterY;
            float _Radius;
            float _Progress;
            
            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // 计算中心点
                float2 center = float2(_CenterX, _CenterY);
                
                // 计算到中心点的距离
                float dist = distance(i.texcoord, center);
                
                // 根据进度调整实际半径 - 使用更明显的放大效果
                // 当Progress=0时，半径=_Radius
                // 当Progress=1时，半径=1.0（覆盖整个屏幕）
                float actualRadius = lerp(_Radius, 1.0, _Progress);
                
                // 计算可见性：距离小于半径的区域为透明，其他区域为黑色
                float edgeSmoothness = 0.02;
                float visibility = 1.0 - smoothstep(actualRadius - edgeSmoothness, actualRadius + edgeSmoothness, dist);
                
                // 完全黑色，只有可见区域是透明的
                fixed4 col = fixed4(0, 0, 0, 1.0 - visibility);
                
                // 当完全恢复时（Progress=1），使整个屏幕透明
                if (_Progress >= 0.99)
                {
                    col.a = 0.0;
                }
                
                // 应用颜色和纹理
                col *= i.color;
                
                // 应用雾效
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "UI/Default"
}