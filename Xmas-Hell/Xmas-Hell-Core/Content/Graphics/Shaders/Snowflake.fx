sampler TextureSampler : register(s0);

struct PixelShaderInput
{
    float2 TexCoord : TEXCOORD0;
};

float4 PixelShaderFunction(PixelShaderInput input) : COLOR0
{
    return float4(1., 0., 0., 1.);
}

technique AnimatedGradient {
    pass P0 {
        #if SM4
            PixelShader = compile ps_4_0_level_9_1 PixelShaderFunction();
        #elif SM3
            PixelShader = compile ps_3_0 PixelShaderFunction();
        #else
            PixelShader = compile ps_2_0 PixelShaderFunction();
        #endif
    }
}
