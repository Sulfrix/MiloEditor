#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

layout(binding = 0, std140) uniform ProjectionMatrixBuffer
{
    mat4 projection_matrix;
    mat4 model_matrix;
} _16;

layout(location = 0) in vec3 in_position;
out vec3 normal;
layout(location = 1) in vec3 in_normal;
out vec3 updir;
out vec2 texCoord;
layout(location = 2) in vec2 in_uv;

void main()
{
    gl_Position = (_16.projection_matrix * _16.model_matrix) * vec4(in_position, 1.0);
    normal = in_normal;
    updir = vec3(0.0, 0.0, 1.0);
    texCoord = in_uv;
}

