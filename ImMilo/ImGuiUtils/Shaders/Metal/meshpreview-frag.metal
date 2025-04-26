#pragma clang diagnostic ignored "-Wmissing-prototypes"

#include <metal_stdlib>
#include <simd/simd.h>

using namespace metal;

struct UniformBuffer
{
    uint hasTexture;
};

struct FS_out
{
    float4 outputColor [[color(0)]];
};

struct FS_in
{
    float3 normal [[user(locn0)]];
    float3 updir [[user(locn1)]];
    float2 texCoord [[user(locn2)]];
};

static inline __attribute__((always_inline))
float map(thread const float& value, thread const float& inMin, thread const float& inMax, thread const float& outMin, thread const float& outMax)
{
    return outMin + (((outMax - outMin) * (value - inMin)) / (inMax - inMin));
}

fragment FS_out FS(FS_in in [[stage_in]], constant UniformBuffer& _39 [[buffer(1)]], texture2d<float> DiffuseTexture [[texture(0)]], sampler TextureSampler [[sampler(2)]])
{
    FS_out out = {};
    float4 diffuseColor = float4(1.0);
    if (_39.hasTexture != 0u)
    {
        diffuseColor = DiffuseTexture.sample(TextureSampler, in.texCoord);
    }
    float param = dot(in.normal, in.updir);
    float param_1 = -1.0;
    float param_2 = 1.0;
    float param_3 = 0.20000000298023223876953125;
    float param_4 = 1.0;
    float lightval = map(param, param_1, param_2, param_3, param_4);
    out.outputColor = float4(float3(lightval) * float3(diffuseColor.xyz), diffuseColor.w);
    return out;
}

