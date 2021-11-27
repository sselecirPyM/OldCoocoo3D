struct VSSkinnedIn
{
	float4 Pos	: POSITION;			//Position
};

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};

PSSkinnedIn vsmain(VSSkinnedIn input)
{
	PSSkinnedIn output;
	output.Pos = float4(input.Pos.xyz, 1);
	output.uv = input.Pos.xy*0.5f+0.5f;
	output.uv.y = 1 - output.uv.y;

	return output;
}

Texture2D texture0 :register(t0);
SamplerState s0 : register(s0);
SamplerState s3 : register(s3);

float4 psmain(PSSkinnedIn input) : SV_TARGET
{
	float4 sourceColor = texture0.Sample(s3, input.uv);
	float3 color = sourceColor.rgb;
	color = max(0.001, color);
	color = pow(color.rgb, 1 / 2.2);
	return float4(color, sourceColor.a);
}