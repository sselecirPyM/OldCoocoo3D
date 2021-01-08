struct VSSkinnedIn
{
	float4 Pos	: POSITION;			//Position
};

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};

PSSkinnedIn main(VSSkinnedIn input)
{
	PSSkinnedIn output;
	output.Pos = float4(input.Pos.xyz, 1);
	output.uv = input.Pos.xy*0.5f+0.5f;
	output.uv.y = 1 - output.uv.y;

	return output;
}