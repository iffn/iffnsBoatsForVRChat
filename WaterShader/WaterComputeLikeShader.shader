Shader "iffnsShaders/WaterShader/WaterComputeLikeShader"
{
    

    Properties
    {
        phaseVelocitySquared("Phase velocity squared", Range(0.0001, 1)) = 0.02
        attenuation("Attenuation", Range(0.0001, 1)) = 0.999
        _depthTexture("DepthTexture", 2D) = "white"
        //OtherPublicParameterDefinitions
    }

    CGINCLUDE

    #include "UnityCustomRenderTexture.cginc"
    
    #define A(U)  tex2D(_SelfTexture2D, float2(U))

    float phaseVelocitySquared = 0.02;
    float attenuation = 0.999;
    sampler2D _depthTexture;

    //OtherParameterDefinitions

    float4 frag(v2f_customrendertexture i) : SV_Target
    {
        float2 uv = i.globalTexcoord;

        float du = 1.0 / _CustomRenderTextureWidth;
        float dv = 1.0 / _CustomRenderTextureHeight;
        float4 duv = float4(du, dv, 0 ,0);
        
        float4 cell = A(uv);
        float4 cellUp = A(uv + duv.wy);
        float4 cellDown = A(uv - duv.wy);
        float4 cellRight = A(uv + duv.xw);
        float4 cellLeft = A(uv - duv.xw);

        //Calculation functions:

        //Drop waves https://github.com/hecomi/UnityWaterSurface/blob/master/Assets/WaterSimulation.shader
        

        //r = current state
        //g = previous state

        float2 prevState = tex2D(_SelfTexture2D, uv);
        float newState = (2 * prevState.r - prevState.g + phaseVelocitySquared * (
            tex2D(_SelfTexture2D, uv - duv.zy).r +
            + tex2D(_SelfTexture2D, uv + duv.zy).r +
            + tex2D(_SelfTexture2D, uv - duv.xz).r +
            + tex2D(_SelfTexture2D, uv + duv.xz).r
            - 4 * prevState.r)
            ) * attenuation;

        float4 returnValue = float4(newState, prevState.r, 0, 0);
        
        float2 uvDepth = float2(-uv.x + 1, uv.y);

        float depthValueRaw = tex2D(_depthTexture, uvDepth);

        returnValue = saturate(sign(depthValueRaw) + returnValue);//
        
        /*
        //Basic diffuse:
        float baseValue = (cellUp + cellDown + cellRight + cellLeft) * 0.25f;
        float4 returnValue = float4(baseValue, 0, 0, 0);
        */

        /*
        //Sin waves:
        float currentTimeS = _Time.y;
        float baseHeight = sin(currentTimeS + (uv.x * 10)) * 0.5 + 0.5;
        float4 returnValue = float4(baseHeight, 0, 0, 0)
        */
        
        return returnValue;
    }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "Update"
            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            ENDCG
        }
    }
}
