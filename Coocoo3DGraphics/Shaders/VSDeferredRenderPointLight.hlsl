#include "CameraDataDefine.hlsli"
struct LightInfo
{
	float3 LightPos;
	uint LightType;
	float4 LightColor;
	float LightRange;
};
cbuffer cb1 : register(b1)
{
	float4x4 LightSpaceMatrices[2];
	LightInfo Lightings[1];
};
cbuffer cb2 : register(b2)
{
	CAMERA_DATA_DEFINE;//is a macro
};

struct VSIn
{
	float3 Pos	: POSITION;			//Position
};

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float3 wPos	: TEXCOORD;			//world space Pos
};

PSIn main(VSIn input)
{
	PSIn output;
	float4 lightColor = Lightings[0].LightColor;
	float3 Pos = input.Pos * Lightings[0].LightRange * 2 + Lightings[0].LightPos;
	output.Pos = mul(float4(Pos, 1), g_mWorldToProj);
	output.wPos = Pos;
	//output.uv.y/= g_aspectRatio;

	return output;
}