#pragma target 4.5
#pragma editor_sync_compilation
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

Texture2D _BlitTexture;
SamplerState sampler_LinearClamp;

uniform Texture2D _CheckerTexture;
uniform int _DisplayChecker;

uniform float4 _BlitScaleBias;
uniform float4 _BlitScaleBiasRt;
uniform float _BlitMipLevel;

struct Attributes
{
    uint vertexID : SV_VertexID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 texCoord : TEXCOORD0;
    float2 blitScaleTexCoord : TEXCOORD1;
};

Varyings Vertex(Attributes input)
{
    Varyings output;
    output.positionCS = GetQuadVertexPosition(input.vertexID) * float4(_BlitScaleBiasRt.x, _BlitScaleBiasRt.y, 1, 1) + float4(_BlitScaleBiasRt.z, _BlitScaleBiasRt.w, 0, 0);
    output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1

    #if defined(FLIP_GEOMETRY_VERTICAL)
    if (_DisplayChecker == 0)
    {
        output.positionCS.y *= -1;
    }
    #endif

    output.texCoord = GetQuadTexCoord(input.vertexID);
    output.blitScaleTexCoord = GetQuadTexCoord(input.vertexID) * _BlitScaleBias.xy + _BlitScaleBias.zw;
    return output;
}

float4 Fragment(Varyings input) : SV_Target
{
    if (_DisplayChecker == 1)
    {
        float2 texCoord = input.texCoord.xy;

        #if !defined(FLIP_GEOMETRY_VERTICAL)
        texCoord.y = 1 - texCoord.y;
        #endif

        texCoord *= 0.5;

        return SAMPLE_TEXTURE2D(_CheckerTexture, sampler_LinearClamp, texCoord);
    }

    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.blitScaleTexCoord.xy);
}
