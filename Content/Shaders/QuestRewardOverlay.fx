// Quest reward cinematic veil and judgment flash.

float4x4 MatrixTransform;
float2 ViewportSize;
float Time = 0.0;
float OverlayAlpha = 1.0;
float FlashProgress = -1.0;

texture Texture : register(t0);
sampler2D TextureSampler : register(s0) = sampler_state
{
    Texture = <Texture>;
    MinFilter = Linear; MagFilter = Linear; MipFilter = Linear;
    AddressU = Clamp; AddressV = Clamp;
};

struct VSInput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
struct VSOutput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };

VSOutput SpriteVertexShader(VSInput input)
{
    VSOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TexCoord = input.TexCoord;
    return output;
}

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float VerticalVeil(float y)
{
    float top = lerp(0.88, 0.38, smoothstep(0.0, 0.19, y));
    float middle = lerp(0.38, 0.46, smoothstep(0.19, 0.78, y));
    float bottom = lerp(0.46, 0.90, smoothstep(0.78, 1.0, y));
    return y < 0.19 ? top : (y < 0.78 ? middle : bottom);
}

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float2 uv = input.TexCoord;
    float4 original = tex2D(TextureSampler, uv);
    float luminance = dot(original.rgb, float3(0.299, 0.587, 0.114));
    float3 scene = lerp(luminance.xxx, original.rgb, 0.56);
    scene = saturate((scene - 0.5) * 1.04 + 0.5);
    float vertical = VerticalVeil(uv.y);
    float2 radialUv = (uv - float2(0.5, 0.48)) / float2(0.50, 0.48);
    float radial = 0.50 * smoothstep(0.32, 0.87, length(radialUv));
    float veil = 1.0 - (1.0 - vertical) * (1.0 - radial);

    float2 bloomUv = (uv - float2(0.5, 0.093)) / float2(0.29, 0.30);
    float bloom = 0.20 * (1.0 - smoothstep(0.0, 0.92, length(bloomUv)));

    float grain = Hash21(uv * ViewportSize + floor(Time * 24.0) * 17.0);
    float grainShift = (grain - 0.5) * 0.035;

    float flash = 0.0;
    if (FlashProgress >= 0.0 && FlashProgress <= 1.0)
    {
        float p = saturate(FlashProgress);
        float flashAlpha = p < 0.18
            ? smoothstep(0.0, 0.18, p) * 0.95
            : (1.0 - smoothstep(0.18, 1.0, p)) * 0.95;
        float flashScale = lerp(0.4, 2.2, 1.0 - pow(1.0 - p, 3.0));
        float2 centered = (uv - 0.5) / max(flashScale, 0.001);
        float verticalLine = 1.0 - smoothstep(0.0018, 0.012, abs(centered.x));
        float horizontalLine = 1.0 - smoothstep(0.0018, 0.012, abs(centered.y));
        flash = max(verticalLine * 0.84, horizontalLine * 0.59) * flashAlpha;
    }

    float3 blood = float3(0.769, 0.118, 0.227);
    scene *= 1.0 - saturate(veil);
    scene = 1.0 - (1.0 - scene) * (1.0 - blood * saturate(bloom + flash));
    scene = saturate(scene + grainShift * 0.12);
    float3 composited = lerp(original.rgb, scene, saturate(OverlayAlpha));
    return float4(composited, original.a) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
