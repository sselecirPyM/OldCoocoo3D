struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv : TEXCOORD;
};

float luminance(float3 rgb)
{
	return dot(rgb, float3(0.299f, 0.587f, 0.114f));
}

float3 TonemapACES(float3 x)
{
	const float A = 2.51f;
	const float B = 0.03f;
	const float C = 2.43f;
	const float D = 0.59f;
	const float E = 0.14f;
	return (x * (A * x + B)) / (x * (C * x + D) + E);
}

float3 TonemapHable(float3 x)
{
	const float A = 0.22;
	const float B = 0.30;
	const float C = 0.10;
	const float D = 0.20;
	const float E = 0.01;
	const float F = 0.30;
	return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

Texture2D texture0 :register(t0);
SamplerState s0 : register(s0);
SamplerState s3 : register(s3);

float4 main(PSSkinnedIn input) : SV_TARGET
{
	float4 sourceColor = texture0.Sample(s3, input.uv);
	float3 color = sourceColor.rgb;
	color = max(0.001, color);
	color = pow(color.rgb, 1 / 2.2);
	return float4(color, sourceColor.a);
}