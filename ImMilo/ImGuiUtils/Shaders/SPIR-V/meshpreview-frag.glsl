#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout(set = 1, binding = 0) uniform texture2D DiffuseTexture;
layout(set = 0, binding = 1) uniform UniformBuffer {
    bool hasTexture;
};
layout(set = 0, binding = 2) uniform sampler TextureSampler;

//layout (location = 0) in vec4 color;
layout (location = 0) in vec3 normal;
layout (location = 1) in vec3 updir;
layout (location = 2) in vec2 texCoord;
layout (location = 0) out vec4 outputColor;

float map(float value, float inMin, float inMax, float outMin, float outMax) {
  return outMin + (outMax - outMin) * (value - inMin) / (inMax - inMin);
}

vec2 map(vec2 value, vec2 inMin, vec2 inMax, vec2 outMin, vec2 outMax) {
  return outMin + (outMax - outMin) * (value - inMin) / (inMax - inMin);
}

vec3 map(vec3 value, vec3 inMin, vec3 inMax, vec3 outMin, vec3 outMax) {
  return outMin + (outMax - outMin) * (value - inMin) / (inMax - inMin);
}

vec4 map(vec4 value, vec4 inMin, vec4 inMax, vec4 outMin, vec4 outMax) {
  return outMin + (outMax - outMin) * (value - inMin) / (inMax - inMin);
}

void main()
{
    //outputColor = color * texture(sampler2D(FontTexture, FontSampler), texCoord);
    //outputColor = vec4(1.0f, 0.0f, 0.0f, 1.0f);
    //float lightval = map(dot(normal, vec3(0.0f, 0.0f, 1.0f)), -1.0f, 1.0f, 0.2f, 1.0f);
    vec4 diffuseColor = vec4(1.0f, 1.0f, 1.0f, 1.0f);
    if (hasTexture) {
        diffuseColor = texture(sampler2D(DiffuseTexture, TextureSampler), texCoord);
    }
    float lightval = map(dot(normal, updir), -1.0f, 1.0f, 0.2f, 1.0f);
    outputColor = vec4(vec3(lightval) * vec3(diffuseColor), diffuseColor.a);
    //outputColor = vec4(normal, 1.0f);
}