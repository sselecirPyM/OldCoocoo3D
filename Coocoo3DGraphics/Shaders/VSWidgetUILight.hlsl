cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_vTest;
	float4 lightPosRange[512];
}
struct VSIn
{
	float4 Pos	: POSITION;			//Position
	uint instance : SV_InstanceID;
};

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float3 wPos	: TEXCOORD0;
};

PSIn main(VSIn input)
{
	PSIn output;
	float3 Pos = input.Pos * lightPosRange[input.instance].w * 2 + lightPosRange[input.instance].xyz;
	output.Pos = mul(float4(Pos, 1), g_mWorldToProj);
	output.wPos = Pos;

	return output;
}