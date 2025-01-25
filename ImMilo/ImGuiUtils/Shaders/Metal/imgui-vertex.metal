#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct ProjectionMatrixBuffer
{
    float4x4 projection_matrix;
};

struct main0_out
{
    float4 color [[user(locn0)]];
    float2 texCoord [[user(locn1)]];
    float4 gl_Position [[position]];
};

struct main0_in
{
    float2 in_position [[attribute(0)]];
    float2 in_texCoord [[attribute(1)]];
    float4 in_color [[attribute(2)]];
};

vertex main0_out main0(main0_in in [[stage_in]], constant ProjectionMatrixBuffer& _16 [[buffer(0)]])
{
    main0_out out = {};
    out.gl_Position = _16.projection_matrix * float4(in.in_position, 0.0, 1.0);
    out.color = in.in_color;
    out.texCoord = in.in_texCoord;
    return out;
}

