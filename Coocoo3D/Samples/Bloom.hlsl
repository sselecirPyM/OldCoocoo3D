cbuffer cb0 : register(b0)
{
	int2 textureSize;
	float threshold;
	float intensity;
}
Texture2D texture0 : register(t0);
SamplerState s0 : register(s0);
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
	output.uv = -output.uv * 0.5 + 0.5;
	output.uv.x = 1 - output.uv.x;

	return output;
}
const static uint countOfWeights = 16;
const static float weights[16] = {
0.07994048,
0.078357555,
0.073794365,
0.0667719,
0.058048703,
0.048486352,
0.03891121,
0.03000255,
0.022226434,
0.015820118,
0.010818767,
0.0071084364,
0.0044874395,
0.0027217707,
0.0015861068,
0.0008880585,
};
#ifdef BLOOM_1
float4 psmain(PSIn input) : SV_TARGET
{
	float2 offset = float2(1, 0) / textureSize;
	float2 coords = input.uv;
	float4 color = 0;
	for (int i = countOfWeights - 1; i > 0; i--)
	{
		color += max(texture0.SampleLevel(s0, coords - i * offset, 0) - threshold, 0) * weights[i];
	}
	for (int i = 0; i < countOfWeights; i++)
	{
		color += max(texture0.SampleLevel(s0, coords + i * offset, 0) - threshold, 0) * weights[i];
	}

	return color;
}
#endif
#ifdef BLOOM_2
float4 psmain(PSIn input) : SV_TARGET
{
	float2 offset = float2(0, 2) / textureSize;
	float2 coords = input.uv;

	float4 color = 0;
	for (int i = countOfWeights - 1; i > 0; i--)
	{
		color += texture0.SampleLevel(s0, coords - i * offset, 0) * weights[i];
	}
	for (int i = 0; i < countOfWeights; i++)
	{
		color += texture0.SampleLevel(s0, coords + i * offset, 0) * weights[i];
	}
	color.a = 1;
	return color * intensity;
}
#endif