struct UIInfo
{
	float2 size;
	float2 offset;
	float2 uvSize;
	float2 uvOffset;
};
cbuffer cb0 : register(b0)
{
	float2 _ScreenSize;
	float2 preserved1;
	float4 preserved2;
	UIInfo _uiInfo[31];
}
struct VSIn
{
	float4 Pos	: POSITION;			//Position
	uint instance : SV_InstanceID;
};

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};

PSIn main(VSIn input)
{
	PSIn output;
	UIInfo data = _uiInfo[input.instance];
	output.Pos = float4(input.Pos.xyz, 1);
	output.Pos.xy = ((input.Pos.xy + float2(1, 1)) / 2 * data.size + data.offset) * 2 / _ScreenSize - float2(1, 1);

	output.uv = (input.Pos.xy * 0.5f + 0.5f);
	output.uv.y = 1 - output.uv.y;
	output.uv = output.uv * data.uvSize + data.uvOffset;

	return output;
}