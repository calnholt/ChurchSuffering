// Final composite for isolated Climb choice layers.
float4x4 MatrixTransform;
float GrayscaleAmount = 0.0;
float SepiaAmount = 0.0;
float Brightness = 1.0;
float Opacity = 1.0;

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

float4 SpritePixelShader(VSOutput input) : COLOR0
{
    float4 source = tex2D(TextureSampler, input.TexCoord) * input.Color;
    float luminance = dot(source.rgb, float3(0.299, 0.587, 0.114));
    float3 gray = luminance.xxx;
    float3 sepia = float3(
        dot(source.rgb, float3(0.393, 0.769, 0.189)),
        dot(source.rgb, float3(0.349, 0.686, 0.168)),
        dot(source.rgb, float3(0.272, 0.534, 0.131)));
    source.rgb = lerp(source.rgb, gray, saturate(GrayscaleAmount));
    source.rgb = lerp(source.rgb, sepia, saturate(SepiaAmount));
    source.rgb *= max(Brightness, 0.0);
    source *= saturate(Opacity);
    return source;
}

technique SpriteDrawing
{
    pass P0
    {
        VertexShader = compile vs_3_0 SpriteVertexShader();
        PixelShader = compile ps_3_0 SpritePixelShader();
    }
}
