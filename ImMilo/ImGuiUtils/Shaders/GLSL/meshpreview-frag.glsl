#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(binding = 1, std140) uniform UniformBuffer
{
    uint hasTexture;
} _39;

uniform sampler2D SPIRV_Cross_CombinedDiffuseTextureTextureSampler;

in vec2 texCoord;
in vec3 normal;
in vec3 updir;
layout(location = 0) out vec4 outputColor;

float map(float value, float inMin, float inMax, float outMin, float outMax)
{
    return outMin + (((outMax - outMin) * (value - inMin)) / (inMax - inMin));
}

void main()
{
    vec4 diffuseColor = vec4(1.0);
    if (_39.hasTexture != 0u)
    {
        diffuseColor = texture(SPIRV_Cross_CombinedDiffuseTextureTextureSampler, texCoord);
    }
    float param = dot(normal, updir);
    float param_1 = -1.0;
    float param_2 = 1.0;
    float param_3 = 0.20000000298023223876953125;
    float param_4 = 1.0;
    float lightval = map(param, param_1, param_2, param_3, param_4);
    outputColor = vec4(vec3(lightval) * vec3(diffuseColor.xyz), diffuseColor.w);
}

