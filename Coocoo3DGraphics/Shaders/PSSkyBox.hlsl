#include "CameraDataDefine.hlsli"

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};
cbuffer cb2 : register(b2)
{
	CAMERA_DATA_DEFINE;//is a macro
};
TextureCube EnvCube : register (t3);
SamplerState s0 : register(s0);

float4 main(PSIn input) : SV_TARGET
{
	float4 vx = mul(float4(input.uv,0,1),g_mProjToWorld);
	float3 viewDir = vx.xyz / vx.w - g_vCamPos;
	return float4(EnvCube.Sample(s0, viewDir).rgb * g_skyBoxMultiple,1);
}