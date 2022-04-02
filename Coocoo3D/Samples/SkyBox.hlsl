struct VSIn
{
	float4 Pos	: POSITION;			//Position
};

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.Pos = float4(input.Pos.xyz, 1);
	output.Pos.z = 1 - 1e-6;
	output.uv = input.Pos.xy;
	//output.uv.y = 1 - output.uv.y;

	return output;
}

cbuffer cb0 : register(b0)
{
	float4x4 g_mProjToWorld;
	float3   g_vCamPos;
	float g_skyBoxMultiple;
	float _Brightness;
};
TextureCube EnvCube : register (t0);
SamplerState s0 : register(s0);

float4 psmain(PSIn input) : SV_TARGET
{
	float4 vx = mul(float4(input.uv,0,1),g_mProjToWorld);
	float3 viewDir = vx.xyz / vx.w - g_vCamPos;
	return float4(EnvCube.Sample(s0, viewDir).rgb * g_skyBoxMultiple * _Brightness, 1);
}