#include "../helpers/CAMetalRoughnessHelper.hlsli"
#include "../helpers/tone_mapping.hlsli"
#include "../helpers/constants.hlsli"
#include "../helpers/GradientSampling.hlsli"

#include "../Shared/MainVertexShader.hlsli"
#include "../Shared/const_layout.hlsli"

#include "../TextureSamplers.hlsli"
#include "../inputlayouts.hlsli"

#include "../Capabilites/Emissive.hlsli"
#include "../Capabilites/Tint.hlsli"

//#include "Helpers.hlsli"

// **************************************************************************************************************************************
// *		PIXEL SHADER CODE
// **************************************************************************************************************************************
GBufferMaterial GetMaterial(in PixelInputType input)
{
    GBufferMaterial material;

    // default values
    material.diffuse = float4(0.2f, 0.2f, 0.2f, 1);
    material.specular = float4(0, 0, 0, 0);
    material.roughness = 1.0f;
    material.metalness = 0.0f;
    material.pixelNormal = input.normal;
    material.maskValue = float4(0, 0, 0, 0);

    float2 texCord = float2(nfmod(input.tex.x, 1), nfmod(input.tex.y, 1));

    if (UseSpecular)
    {
        material.specular.rgb = _linear(SpecularTexture.Sample(SampleType, texCord).rgb);
    }

    if (UseDiffuse)
    {
        float4 diffuseValue = DiffuseTexture.Sample(SampleType, texCord);
        material.diffuse.rgb = _linear(diffuseValue.rgb);
        material.diffuse.a = diffuseValue.a;
    }

    if (UseGloss)
    {
        float4 glossTexSample = GlossTexture.Sample(SampleType, texCord);
        material.metalness = (glossTexSample.r);
        material.roughness = (glossTexSample.g);

    }

    if (UseNormal)
    {
        material.pixelNormal = GetPixelNormal(input);
    }

    if (UseMask)
    {
        material.maskValue = MaskTexture.Sample(SampleType, texCord);
    }

    return material;
}

float4 DefaultPixelShader(in PixelInputType input, bool bIsFrontFace : SV_IsFrontFace) : SV_TARGET0
{
    GBufferMaterial material = GetMaterial(input);

    if (UseAlpha == 1)
        alpha_test(material.diffuse.a);

    float3 normalizedViewDirection = -normalize(CameraPos - input.worldPosition);
    float3 rotatedNormalizedLightDirection = normalize(mul(light_Direction_Constant, (float3x3) DirLightTransform));

    const float occlusion = 1.0f;

    R2_5_StandardLightingModelMaterial_For_GBuffer standard_mat_compressed =
        R2_5_create_standard_lighting_material_for_gbuffer(
        material.diffuse.rgb,
        material.pixelNormal,
        material.roughness,
        material.metalness,
        occlusion);

    R2_5_StandardLightingModelMaterial_For_Lighting slm_uncompressed = R2_5_get_slm_for_lighting(standard_mat_compressed);
    slm_uncompressed.Diffuse_Colour.rgb = ApplyTintAndFactionColours(slm_uncompressed.Diffuse_Colour.rgb, material.maskValue);

    // ====== FOUR-STUDIO-LIGHT SETUP ======
    float3x3 lightRot = (float3x3)DirLightTransform;
    const float studioLightScale = 60000.0f;

    float3 L[4];
    L[0] = normalize(mul(normalize(float3(-0.35f, 0.17f, -0.92f)), lightRot));
    L[1] = normalize(mul(normalize(float3(-0.41f, 0.35f,  0.84f)), lightRot));
    L[2] = normalize(mul(normalize(float3( 0.52f, 0.83f,  0.21f)), lightRot));
    L[3] = normalize(mul(normalize(float3( 0.62f, -0.56f, -0.54f)), lightRot));

    float3 lightCol[4];
    lightCol[0] = float3(0.03f, 0.03f, 0.03f) * studioLightScale;
    lightCol[1] = float3(0.52f, 0.54f, 0.54f) * studioLightScale;
    lightCol[2] = float3(0.04f, 0.03f, 0.05f) * studioLightScale;
    lightCol[3] = float3(0.09f, 0.08f, 0.07f) * studioLightScale;

    float3 dir_light = float3(0, 0, 0);
    float3 R = reflect(normalizedViewDirection, slm_uncompressed.Normal);

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        dir_light += standard_lighting_model_directional_light_SM4_private(
            lightCol[i],
            L[i],
            normalizedViewDirection,
            R,
            slm_uncompressed);
    }

    float3 env_light = standard_lighting_model_environment_light_SM4_private(
        normalizedViewDirection,
        R,
        slm_uncompressed);

    float3 hdr_linear_col = env_light + dir_light;
    float3 ldr_linear_col = saturate(Uncharted2ToneMapping(hdr_linear_col));

    float3 emissiveColour = GetEmissiveColour(input.tex, material.maskValue, rotatedNormalizedLightDirection, material.pixelNormal);

    float3 color = ldr_linear_col + emissiveColour;
    return float4(_gamma(color), 1.0f);
}

float4 EmissiveLayerPixelShader(in PixelInputType input, bool bIsFrontFace : SV_IsFrontFace) : SV_TARGET0
{
    GBufferMaterial material = GetMaterial(input);
    float3 normlizedViewDirection = normalize(input.viewDirection);
    float3 emissiveColour = GetEmissiveColour(input.tex, material.maskValue, normlizedViewDirection, material.pixelNormal);

    if (UseAlpha == 1)
        alpha_test(material.diffuse.a);

    return float4(emissiveColour, 1);
}

technique BasicColorDrawing
{
    pass P0
    {
        VertexShader = compile vs_5_0 MainVertexShader();
        PixelShader = compile ps_5_0 DefaultPixelShader();
    }
};

technique GlowDrawing
{
    pass P0
    {
        VertexShader = compile vs_5_0 MainVertexShader();
        PixelShader = compile ps_5_0 EmissiveLayerPixelShader();
    }
};