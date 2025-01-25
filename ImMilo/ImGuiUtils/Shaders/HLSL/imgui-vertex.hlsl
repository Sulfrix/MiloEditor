cbuffer ProjectionMatrixBuffer : register(b0)
{
    row_major float4x4 _16_projection_matrix : packoffset(c0);
};

uniform float4 gl_HalfPixel;

static float4 gl_Position;
static float2 in_position;
static float4 color;
static float4 in_color;
static float2 texCoord;
static float2 in_texCoord;

struct SPIRV_Cross_Input
{
    float2 in_position : TEXCOORD0;
    float2 in_texCoord : TEXCOORD1;
    float4 in_color : TEXCOORD2;
};

struct SPIRV_Cross_Output
{
    float4 color : TEXCOORD0;
    float2 texCoord : TEXCOORD1;
    float4 gl_Position : POSITION;
};

void vert_main()
{
    gl_Position = mul(float4(in_position, 0.0f, 1.0f), _16_projection_matrix);
    color = in_color;
    texCoord = in_texCoord;
    gl_Position.x = gl_Position.x - gl_HalfPixel.x * gl_Position.w;
    gl_Position.y = gl_Position.y + gl_HalfPixel.y * gl_Position.w;
}

SPIRV_Cross_Output main(SPIRV_Cross_Input stage_input)
{
    in_position = stage_input.in_position;
    in_color = stage_input.in_color;
    in_texCoord = stage_input.in_texCoord;
    vert_main();
    SPIRV_Cross_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output.color = color;
    stage_output.texCoord = texCoord;
    return stage_output;
}
