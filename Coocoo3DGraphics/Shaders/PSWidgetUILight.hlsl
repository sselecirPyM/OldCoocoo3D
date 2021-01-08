struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float3 wPos	: TEXCOORD;			//world space Pos
};

float4 main(PSIn input) : SV_TARGET
{
	float4 color = float4(1, 1, 1, 1);
	return color;
}