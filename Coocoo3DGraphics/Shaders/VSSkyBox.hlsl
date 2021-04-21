struct VSIn
{
	float4 Pos	: POSITION;			//Position
};

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};

PSIn main(VSIn input)
{
	PSIn output;
	output.Pos = float4(input.Pos.xyz, 1);
	output.Pos.z = 1-1e-6;
	output.uv = input.Pos.xy;
	//output.uv.y = 1 - output.uv.y;

	return output;
}