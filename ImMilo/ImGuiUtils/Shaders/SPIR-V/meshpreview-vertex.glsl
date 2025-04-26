#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 in_position;
layout (location = 1) in vec3 in_normal;
layout (location = 2) in vec2 in_uv;
//layout (location = 2) in vec4 in_color;

layout (binding = 0) uniform ProjectionMatrixBuffer
{
    mat4 projection_matrix;
    mat4 model_matrix;
};

//layout (location = 0) out vec4 color;
layout (location = 0) out vec3 normal;
layout (location = 1) out vec3 updir;
layout (location = 2) out vec2 texCoord;

out gl_PerVertex
{
    vec4 gl_Position;
};

void main() 
{
    gl_Position = projection_matrix * model_matrix * vec4(in_position, 1);
    normal = in_normal;
    updir = vec3(0, 0, 1);
    //color = in_color;
    texCoord = in_uv;
}
