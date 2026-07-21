float4x4 MatrixTransform;
float LifecycleAlpha = 1.0;
float BaseZoom = 1.0;
float ModalZoom = 1.004;
float BaseSaturation = 0.90;
float ModalSaturation = 0.62;
float BaseContrast = 1.02;
float ModalContrast = 1.05;
float2 ParallaxOffset = float2(0.0, 0.0);
float VignetteStrength = 1.0;
float DimStrength = 1.0;

texture Texture : register(t0);
sampler2D TextureSampler : register(s0) = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VSOutput SpriteVertexShader(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

float VerticalDim(float y)
{
    float top = lerp(0.78, 0.42, smoothstep(0.0, 0.18, y));
    float middle = lerp(0.42, 0.46, smoothstep(0.18, 0.80, y));
    return y < 0.18 ? top : lerp(middle, 0.86, smoothstep(0.80, 1.0, y));
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float alpha = saturate(LifecycleAlpha);
    float zoom = max(lerp(BaseZoom, ModalZoom, alpha), 0.001);
    float2 uv = (input.TexCoord - 0.5) / zoom + 0.5 + ParallaxOffset;
    float4 sampled = tex2D(TextureSampler, uv);

    float saturation = lerp(BaseSaturation, ModalSaturation, alpha);
    float contrast = lerp(BaseContrast, ModalContrast, alpha);
    float luminance = dot(sampled.rgb, float3(0.299, 0.587, 0.114));
    float3 color = lerp(luminance.xxx, sampled.rgb, saturation);
    color = saturate((color - 0.5) * contrast + 0.5);

    float2 radialUv = (input.TexCoord - float2(0.5, 0.42)) / float2(0.62, 0.62);
    float radial = lerp(0.0, 0.90, smoothstep(0.24, 1.0, length(radialUv)));
    float vertical = VerticalDim(input.TexCoord.y);
    float dim = 1.0 - (1.0 - vertical) * (1.0 - radial);
    color *= 1.0 - saturate(dim * DimStrength * alpha);

    float2 vignetteUv = (input.TexCoord - 0.5) / float2(0.70, 0.58);
    float vignette = smoothstep(0.55, 1.15, length(vignetteUv));
    color *= 1.0 - vignette * VignetteStrength * 0.35 * alpha;

    return float4(color, sampled.a) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
