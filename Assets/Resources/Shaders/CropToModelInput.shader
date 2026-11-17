// Copies a region of the source texture (the centered square of the camera image) into the model input,
// optionally converting linear colors back to sRGB. Used by CameraFrameProvider through Graphics.Blit.
Shader "Hidden/CuidAR/CropToModelInput"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Crop;   // xy = size of the region, zw = its offset (in UV)
            float _ToSRGB;
            float _FlipY;

            float4 frag (v2f_img i) : SV_Target
            {
                float2 src = i.uv;
                if (_FlipY > 0.5)
                    src.y = 1.0 - src.y;
                float2 uv = src * _Crop.xy + _Crop.zw;
                float3 color = tex2D(_MainTex, uv).rgb;
                if (_ToSRGB > 0.5)
                    color = LinearToGammaSpace(color);
                return float4(color, 1.0);
            }
            ENDCG
        }
    }
}
