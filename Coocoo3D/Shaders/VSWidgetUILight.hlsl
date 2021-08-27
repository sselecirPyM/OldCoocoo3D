cbuffer cb0 : register(b0)
{
	float4x4 g_mObjectToProj;
	//float4x4 g_mObjectToWorld;
}
struct VSIn
{
	float4 Pos	: POSITION;			//Position
	uint instance : SV_InstanceID;
};

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	//float3 wPos	: TEXCOORD0;
};

PSIn main(VSIn input)
{
	PSIn output;
	//float3 Pos = input.Pos * boundingBox.extension + boundingBox.position;
	output.Pos = mul(float4(input.Pos.xyz, 1), g_mObjectToProj);
	//output.wPos = Pos;

	return output;
}