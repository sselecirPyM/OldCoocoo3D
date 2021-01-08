cbuffer cb1 : register(b1)
{
	float4x4 g_mWorld;
};
cbuffer cb2 : register(b2)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_vCamPos;
	float g_aspectRatio;
	float g_time;
	float g_deltaTime;
	uint2 g_camera_randomValue;
	float g_skyBoxMultiple;
	float3 g_camera_preserved;
	float4 g_camera_preserved2[5];
};

struct VSSkinnedOut
{
	float4 Pos	: SV_POSITION;		//Position
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
	float EdgeScale : EDGESCALE;
};

[maxvertexcount(3)]
void main(
	triangle VSSkinnedOut input[3],
	inout TriangleStream< VSSkinnedOut > triStream
)
{
	VSSkinnedOut output;
	float3 norm = normalize(cross(input[0].Pos.xyz - input[1].Pos.xyz, input[1].Pos.xyz - input[2].Pos.xyz));
	for (int i = 0; i < 3; i++)
	{
		output = input[i];
		output.Pos = input[i].Pos + float4((abs(g_time % 4 - 2) / 4) * norm, 0);

		triStream.Append(output);
	}
	triStream.RestartStrip();
}