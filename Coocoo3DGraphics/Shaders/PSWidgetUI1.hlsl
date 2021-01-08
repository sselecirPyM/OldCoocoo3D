Texture2D texture0 :register(t0);
SamplerState s0 : register(s0);
struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv : TEXCOORD;
};

float4 main(PSIn input) : SV_TARGET
{
	//return float4(1,0,1,1);
	float4 color = texture0.Sample(s0, input.uv);
	//clip(color.a - 0.5);
	return color;
}