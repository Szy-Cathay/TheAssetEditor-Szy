// Vertex point shader for rendering edit mode vertices as camera-facing circular points.
// Based on Blender's overlay_edit_mesh_vert.glsl approach.
// Renders instanced quads as billboarded circles with Z-bias for selected vertices.

#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 View;
float4x4 ViewProjection;
float3 CameraPosition;

// Instance data: position, scale, color, and selection weight
struct VSInstanceInput
{
    float3 InstancePosition : POSITION1;
    float InstanceScale : NORMAL1;
    float3 InstanceColor : NORMAL2;
    float InstanceWeight : NORMAL3;
};

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
    float Weight : TEXCOORD1;
};

// Vertex shader: billboard quad with screen-space size
VSOutput VertexPointVS(VSInput input, VSInstanceInput instance)
{
    VSOutput output = (VSOutput)0;

    // Get camera right and up vectors from view matrix for billboard orientation
    float3 cameraRight = float3(View[0][0], View[1][0], View[2][0]);
    float3 cameraUp = float3(View[0][1], View[1][1], View[2][1]);

    // Offset billboard center slightly toward camera (in world space)
    // This prevents the quad from extending behind the surface and being half-clipped
    float3 toCamera = normalize(CameraPosition - instance.InstancePosition);
    float3 adjustedPos = instance.InstancePosition + toCamera * 0.005 * instance.InstanceScale;

    // Billboard offset from center
    float3 offset = (input.Position.xyz * instance.InstanceScale);

    // Apply billboard rotation (already in camera space)
    float3 worldPos = adjustedPos
        + cameraRight * offset.x
        + cameraUp * offset.y;

    // Transform to clip space
    output.Position = mul(float4(worldPos, 1.0), ViewProjection);
    output.TexCoord = input.TexCoord;
    output.Color = float4(instance.InstanceColor, 1.0);
    output.Weight = instance.InstanceWeight;

    // Small Z-bias for selected vertices only (like Blender: 5e-7 * abs(w))
    if (instance.InstanceWeight > 0.5)
    {
        output.Position.z -= 0.0000005 * abs(output.Position.w);
    }

    return output;
}

// Pixel shader: circle clipping with anti-aliasing and outline for selected vertices
float4 VertexPointPS(VSOutput input) : COLOR0
{
    // Distance from center (0.5, 0.5) in UV space
    float2 center = float2(0.5, 0.5);
    float dist = length(input.TexCoord - center);

    // Discard pixels outside the circle
    if (dist > 0.5)
        discard;

    // Anti-aliased edge using smoothstep
    float alpha = smoothstep(0.5, 0.45, dist);

    // Selected vertices get an outline ring
    if (input.Weight > 0.5)
    {
        // Draw dark outline ring for selected vertices
        if (dist > 0.35 && dist < 0.48)
        {
            float outlineAlpha = smoothstep(0.35, 0.38, dist) * smoothstep(0.48, 0.45, dist);
            return float4(0.15, 0.15, 0.15, outlineAlpha * alpha);
        }
    }

    return float4(input.Color.rgb, alpha);
}

technique VertexPoint
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL VertexPointVS();
        PixelShader = compile PS_SHADERMODEL VertexPointPS();
    }
}