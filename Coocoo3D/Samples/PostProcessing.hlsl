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
	//output.uv.y = 1 - output.uv.y;

	return output;
}

cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float4x4 g_mWorldToProj1;
	float4x4 g_mProjToWorld1;
	int2 _widthHeight;
	float g_cameraFarClip;
	float g_cameraNearClip;
};
Texture2D _result :register(t0);
Texture2D _depth : register (t1);
Texture2D _previousResult :register(t2);
Texture2D _previousDepth : register (t3);
SamplerState s0 : register(s0);
SamplerState s3 : register(s3);

float getLinearDepth(float z)
{
	float far = g_cameraFarClip;
	float near = g_cameraNearClip;
	return near * far / (far + near - z * (far - near));
}

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.uv * 0.5 + 0.5;
	uv.y = 1 - uv.y;

	float2 pixelSize = 1.0f / _widthHeight;

	float4 sourceColor = _result.SampleLevel(s3, uv, 0);
	float3 color = sourceColor.rgb;

	color = max(0.001, color);
	color = pow(color.rgb, 1 / 2.2);
	return float4(color, sourceColor.a);
}