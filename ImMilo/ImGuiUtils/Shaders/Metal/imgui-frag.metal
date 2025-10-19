#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct spvDescriptorSetBuffer0
{
    sampler FontSampler [[id(1)]];
};

struct spvDescriptorSetBuffer1
{
    texture2d<float> FontTexture [[id(0)]];
};

struct FS_out
{
    float4 outputColor [[color(0)]];
};

struct FS_in
{
    float4 color [[user(locn0)]];
    float2 texCoord [[user(locn1)]];
};

fragment FS_out FS(FS_in in [[stage_in]], constant spvDescriptorSetBuffer0& spvDescriptorSet0 [[buffer(0)]], constant spvDescriptorSetBuffer1& spvDescriptorSet1 [[buffer(1)]])
{
    FS_out out = {};
    out.outputColor = in.color * spvDescriptorSet1.FontTexture.sample(spvDescriptorSet0.FontSampler, in.texCoord);
    return out;
}

