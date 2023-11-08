Shader "Thad/WaterStencil" //WaterStencil shader written by ThadGyther
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [IntRange]_StencilRef("Stencil Reference", Range(1,255)) = 69
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Transparent-1" "ForceNoShadowCasting" = "True"}
        LOD 100

        Pass
        {
            ColorMask 0
            Cull Back
            ZTest LEqual
            ZWrite Off
            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                ZFail Replace
                Pass Zero
            }
        }

        Pass
        {
            ColorMask 0
            Cull Front
            ZTest Always
            ZWrite Off
            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Fail Zero
                Pass Replace
            }
        }
    }
}
