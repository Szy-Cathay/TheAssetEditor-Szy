////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  Screen-space outline post-process for selection highlight
////////////////////////////////////////////////////////////////////////////////////////////////////////////

float2 InverseResolution;
float3 OutlineColor = float3(1.0, 0.5, 0.0);

Texture2D ScreenTexture;

SamplerState LinearSampler
{
    Texture = <ScreenTexture>;

    MagFilter = LINEAR;
    MinFilter = LINEAR;
    Mipfilter = LINEAR;

    AddressU = CLAMP;
    AddressV = CLAMP;
};

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  STRUCTS
////////////////////////////////////////////////////////////////////////////////////////////////////////////

struct VertexShaderInput
{
    float3 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  VERTEX SHADER
////////////////////////////////////////////////////////////////////////////////////////////////////////////

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = float4(input.Position, 1);
    output.TexCoord = input.TexCoord;
    return output;
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  PIXEL SHADER - Screen-space edge detection outline
//  Samples the selection mask in a ring around each pixel.
//  If the center is empty but neighbours have mask content, output outline color.
////////////////////////////////////////////////////////////////////////////////////////////////////////////

float4 OutlinePS(float4 pos : SV_POSITION, float2 texCoord : TEXCOORD0) : SV_TARGET0
{
    float center = ScreenTexture.Sample(LinearSampler, texCoord).a;

    // Inside the selected object - skip
    if (center > 0.5)
        discard;

    // Sample surrounding pixels at distance 1 and 2
    float2 px = InverseResolution;
    float maxAlpha = 0;

    // Ring 1 (1 pixel offset, 8 samples)
    float2 o1 = px;
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2(-o1.x, -o1.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( 0,     -o1.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( o1.x,  -o1.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2(-o1.x,   0)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( o1.x,   0)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2(-o1.x,   o1.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( 0,       o1.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( o1.x,    o1.y)).a);

    // Ring 2 (2 pixels offset, 8 samples)
    float2 o2 = px * 2.0;
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2(-o2.x, -o2.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( 0,     -o2.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( o2.x,  -o2.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2(-o2.x,   0)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( o2.x,   0)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2(-o2.x,   o2.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( 0,       o2.y)).a);
    maxAlpha = max(maxAlpha, ScreenTexture.Sample(LinearSampler, texCoord + float2( o2.x,    o2.y)).a);

    if (maxAlpha > 0.1)
        return float4(OutlineColor, 1.0);

    return float4(0, 0, 0, 0);
}

////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  TECHNIQUES
////////////////////////////////////////////////////////////////////////////////////////////////////////////

technique Outline
{
    pass Pass1
    {
        VertexShader = compile vs_4_0 VertexShaderFunction();
        PixelShader = compile ps_4_0 OutlinePS();
    }
}
