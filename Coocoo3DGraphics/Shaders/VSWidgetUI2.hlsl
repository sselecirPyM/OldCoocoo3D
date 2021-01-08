#define MAX_BONE_MATRICES 1020
struct UIInfo
{
	float2 size;
	float2 offset;
	float2 uvSize;
	float2 uvOffset;
};
cbuffer cb0 : register(b0)
{
	float4x4 g_cameraMatrix;
	float4x4 g_vTest;
	UIInfo _uiInfo;
	float2 _ScreenSize;
	float2 preserved1;
	float4 preserved2[5];
	float4 g_bonePosition[MAX_BONE_MATRICES];
}

cbuffer cbAnimMatrices : register(b1)
{
	float4x4 g_mWorld;
	float4x4 g_bonePreserved2[3];
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};
struct VSIn
{
	float4 Pos	: POSITION;			//Position
	uint instance : SV_InstanceID;
};

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD0;
	float4 otherInfo : TEXCOORD1;
};

PSIn main(VSIn input)
{
	PSIn output;
	UIInfo data = _uiInfo;
	float4 position = float4(g_bonePosition[input.instance].xyz, 1);
	float4 viewPosition = mul(mul(mul(position, g_mConstBoneWorld[input.instance]), g_mWorld), g_cameraMatrix);

	output.Pos = float4(input.Pos.xyz,1);
	output.Pos.xy = viewPosition.xy / viewPosition.w + (input.Pos.xy * data.size + data.offset) / _ScreenSize;
	output.Pos.z = viewPosition.z / viewPosition.w;
	//output.Pos.xy = input.Pos.xy*0.25+ position.xy;

	output.uv = (input.Pos.xy * 0.5f + 0.5f);
	output.uv.y = 1 - output.uv.y;
	output.uv = output.uv * data.uvSize + data.uvOffset;
	output.otherInfo = float4(viewPosition.xy / viewPosition.w, 0, viewPosition.z / viewPosition.w);
	return output;
}