#include "CameraDataDefine.hlsli"
cbuffer cb2 : register(b2)
{
	CAMERA_DATA_DEFINE;//is a macro
};

struct VSSkinnedIn
{
	float3 Pos	: POSITION;			//Position
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tan : TANGENT;		    //Normalized Tangent vector
	float EdgeScale : EDGESCALE;
};

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
	float EdgeScale : EDGESCALE;
};

PSSkinnedIn main(VSSkinnedIn input)
{
	PSSkinnedIn output;

	output.Pos = mul(float4(input.Pos,1), g_mWorldToProj);
	output.wPos = float4(input.Pos,1);
	output.Norm = input.Norm;
	output.Tangent = input.Tan;
	output.Tex = input.Tex;
	output.EdgeScale = input.EdgeScale;

	return output;
}