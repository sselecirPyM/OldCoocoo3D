struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};
cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_vCamPos;
	float g_skyBoxMultiple;
};
TextureCube EnvCube : register (t3);
SamplerState s0 : register(s0);

float4 main(PSIn input) : SV_TARGET
{
	float4 vx = mul(float4(input.uv,0,1),g_mProjToWorld);
	float3 viewDir = vx.xyz / vx.w - g_vCamPos;
	return float4(EnvCube.Sample(s0, viewDir).rgb * g_skyBoxMultiple,1);
}