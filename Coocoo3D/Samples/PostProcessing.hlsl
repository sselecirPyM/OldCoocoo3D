struct VSIn
{
	uint vertexId : SV_VertexID;
};

struct PSIn
{
	float4 position	: SV_POSITION;
	float2 texcoord	: TEXCOORD;
};

PSIn vsmain(VSIn input)
{
	PSIn output;
	output.texcoord = float2((input.vertexId << 1) & 2, input.vertexId & 2);
	output.position = float4(output.texcoord.xy * 2.0 - 1.0, 0.0, 1.0);

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
	float2 uv = input.texcoord;
	uv.y = 1 - uv.y;

	float2 pixelSize = 1.0f / _widthHeight;

	float4 sourceColor = _result.SampleLevel(s3, uv, 0);
	float3 color = sourceColor.rgb;

	color = max(0.001, color);
	color = pow(color.rgb, 1 / 2.2);
	return float4(color, sourceColor.a);
}