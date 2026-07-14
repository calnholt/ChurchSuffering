// Poison card slime effect, converted from poison_card.glsl for the card shader compositor.
float4x4 MatrixTransform;
float2 iResolution;
float iTime;
float BLOB_FREQ = 6.0;
float BLOB_STRETCH = 1.5;
float BLOB_REACH_MIN = 0.55;
float BLOB_REACH_MAX = 0.95;
float THRESH_LO = 0.15;
float THRESH_HI = 0.90;
float FLOW_SPEED = 0.10;
float REFRACT_AMT = 0.16;
float ALPHA_THIN = 0.35;
float ALPHA_THICK = 0.90;
float ABSORB_STR = 0.90;
float3 ABSORB_COLOR = float3(0.30, 0.80, 0.34);
float3 SLIME_SURFACE = float3(0.55, 0.85, 0.30);
float3 SLIME_DEEP = float3(0.00, 0.26, 0.08);
float3 LIGHT_DIR = float3(-0.40, 0.65, 0.80);
float AMBIENT = 0.38;
float DIFFUSE = 0.75;
float SPEC_POWER = 46.0;
float SPEC_INTENSITY = 0.95;
float RIM_POWER = 2.6;
float RIM_INTENSITY = 0.28;

texture Texture : register(t0);
sampler2D TextureSampler : register(s0) = sampler_state { Texture = <Texture>; MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Clamp; AddressV = Clamp; };
struct VSInput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
struct VSOutput { float4 Position : POSITION0; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };
VSOutput SpriteVertexShader(VSInput input) { VSOutput output; output.Position = mul(input.Position, MatrixTransform); output.Color = input.Color; output.TexCoord = input.TexCoord; return output; }

float Hash21(float2 p) { p = frac(p * float2(123.34, 345.45)); p += dot(p, p + 34.345); return frac(p.x * p.y); }
float2 Hash22(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }
float Noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3.0-2.0*f); return lerp(lerp(Hash21(i),Hash21(i+float2(1,0)),f.x),lerp(Hash21(i+float2(0,1)),Hash21(i+float2(1,1)),f.x),f.y); }
float Fbm(float2 p) { float sum=0, amp=.5; [unroll] for(int i=0;i<5;i++){ sum+=amp*Noise(p); p=mul(float2x2(.88,-.48,.48,.88),p)*2; amp*=.5;} return sum; }
float3 SafeNormalize(float3 value) { return value * rsqrt(max(dot(value,value),.0001)); }
float GooField(float2 p) {
    float2 gp=float2(p.x,p.y/max(BLOB_STRETCH,.001))*max(BLOB_FREQ,.001); gp.y+=iTime*FLOW_SPEED*BLOB_FREQ;
    float2 cell=floor(gp); float field=0;
    [unroll] for(int oy=-2;oy<=2;oy++) [unroll] for(int ox=-2;ox<=2;ox++) {
        float2 id=cell+float2(ox,oy); float2 h=Hash22(id); float reach=lerp(BLOB_REACH_MIN,BLOB_REACH_MAX,Hash21(id+31.7));
        float2 c=id+.5+.42*float2(sin(iTime*.45*lerp(.6,1.4,h.x)+h.x*6.2831),sin(iTime*.45*lerp(.6,1.4,h.y)+h.y*6.2831));
        float x=max(0,1-dot(gp-c,gp-c)/max(reach*reach,.001)); field+=x*x*x;
    }
    return smoothstep(THRESH_LO,max(THRESH_HI,THRESH_LO+.001),field)*lerp(.88,1.12,saturate(p.y));
}
float4 SpritePixelShader(VSOutput input) : COLOR0 {
    float4 source=tex2D(TextureSampler,input.TexCoord); if(source.a<=.001) return source;
    float2 p=float2(input.TexCoord.x*1.35,1-input.TexCoord.y); float thickness=GooField(p); float e=.006;
    float3 n=SafeNormalize(float3((GooField(p-float2(e,0))-GooField(p+float2(e,0)))*2.6,(GooField(p-float2(0,e))-GooField(p+float2(0,e)))*2.6,1));
    float2 refracted=saturate(input.TexCoord-float2(n.x,-n.y)*REFRACT_AMT*thickness); float3 bg=tex2D(TextureSampler,refracted).rgb;
    float3 l=SafeNormalize(LIGHT_DIR), h=SafeNormalize(l+float3(0,0,1)); float diff=max(dot(n,l),0), spec=pow(max(dot(n,h),0),SPEC_POWER), rim=pow(1-saturate(n.z),RIM_POWER);
    float alpha=lerp(ALPHA_THIN,ALPHA_THICK,thickness); float3 body=lerp(SLIME_SURFACE,SLIME_DEEP,thickness)*(AMBIENT+DIFFUSE*diff);
    float3 trans=bg*lerp(float3(1,1,1),ABSORB_COLOR,thickness*ABSORB_STR); float3 color=lerp(source.rgb,lerp(trans,body,alpha),thickness);
    color+=float3(1,1,.95)*spec*SPEC_INTENSITY*thickness+float3(.65,1,.55)*rim*RIM_INTENSITY*thickness;
    return float4(saturate(color),source.a)*input.Color;
}
technique SpriteDrawing { pass P0 { VertexShader = compile vs_3_0 SpriteVertexShader(); PixelShader = compile ps_3_0 SpritePixelShader(); } }
