cbuffer cb0 : register(b0)
{
	int2 textureSize;
}
Texture2D texture0 : register(t0);
SamplerState s0 : register(s0);

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};

float4 main(PSIn input) : SV_TARGET
{
	const float weights[9] = { 0.048297,0.08393,0.124548,0.157829,0.170793,0.157829,0.124548,0.08393,0.048297 };
	float2 offset = float2(1, 0) / textureSize;
	float2 coords = (-input.uv * 0.5 + 0.5);
	coords.x = 1 - coords.x;
	coords -= offset * 4.0;

	float4 color = 0;
	for (int i = 0; i < 9; i++)
	{
		color += max(texture0.SampleLevel(s0, coords, 0) - 1.0f,0) * weights[i]; coords += offset;
	}

	return color;
}