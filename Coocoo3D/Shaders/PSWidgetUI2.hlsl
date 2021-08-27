Texture2D texture0 :register(t0);
SamplerState s0 : register(s0);

cbuffer cb0 : register(b0)
{
	float4x4 g_cameraMatrix;
	float4x4 g_pvMatrix;
}

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv : TEXCOORD0;
	float4 otherInfo : TEXCOORD1;
};

float luminance(float3 rgb)
{
	return dot(rgb, float3(0.299f, 0.587f, 0.114f));
}

float4 main(PSIn input) : SV_TARGET
{
	float4 color = texture0.Sample(s0, input.uv);
	float2 texcoord;
	texcoord.x = input.otherInfo.x * 0.5 + 0.5;
	texcoord.y = -input.otherInfo.y * 0.5 + 0.5;
	color.a *= 0.2;
	color.rgb = luminance(color.rgb);
	return color;
}