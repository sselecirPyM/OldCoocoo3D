cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_camPos;
	float g_skyBoxMultiple;
	int2 _widthHeight;

};
Texture2D rayTracingResult :register(t0);
Texture2D gbuffer1 :register(t1);
Texture2D gbufferDepth : register (t2);

SamplerState s0 : register(s0);
SamplerComparisonState sampleShadowMap0 : register(s2);
SamplerState s3 : register(s3);
float3 NormalDecode(float2 enc)
{
	float4 nn = float4(enc * 2, 0, 0) + float4(-1, -1, 1, -1);
	float l = dot(nn.xyz, -nn.xyw);
	nn.z = l;
	nn.xy *= sqrt(max(l, 1e-6));
	return nn.xyz * 2 + float3(0, 0, -1);
}

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};
struct VSIn
{
	float4 Pos	: POSITION;			//Position
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

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.uv * 0.5 + 0.5;
	uv.y = 1 - uv.y;

	float depth0 = gbufferDepth.SampleLevel(s3, uv, 0).r;
	float4 buffer1Color = gbuffer1.SampleLevel(s3, uv, 0);
	float3 outputColor = float3(0, 0, 0);

	outputColor = rayTracingResult.SampleLevel(s3, uv, 0).rgb;
	return float4(outputColor, 0);
}