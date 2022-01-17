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
#if ENABLE_TAA
	float weight = 1;

	float depth2 = _depth.SampleLevel(s0, uv, 0).r;

	float4 wPos2 = mul(float4(input.uv, depth2, 1), g_mProjToWorld);
	wPos2 /= wPos2.w;
	float4 posX2 = mul(wPos2, g_mWorldToProj1);
	float2 uv2 = posX2.xy / posX2.w;
	uv2.x = uv2.x * 0.5 + 0.5;
	uv2.y = 0.5 - uv2.y * 0.5;
	bool aa = false;
	float minz = 1;
	float maxz = 0;
	float minz1 = 1;
	float maxz1 = 0;
	for (int x = -1; x <= 1; x++)
		for (int y = -1; y <= 1; y++)
		{
			float depth = _depth.SampleLevel(s3, uv + float2(x, y) * pixelSize, 0).r;
			minz = min(minz, depth);
			maxz = max(maxz, depth);
			float4 wPos = mul(float4(input.uv + float2(x, y) * pixelSize*2, depth, 1), g_mProjToWorld);
			wPos /= wPos.w;

			float4 posX1 = mul(wPos, g_mWorldToProj1);
			float2 uv1 = posX1.xy / posX1.w;
			uv1.x = uv1.x * 0.5 + 0.5;
			uv1.y = 0.5 - uv1.y * 0.5;

			float depth1 = _previousDepth.SampleLevel(s0, uv2 + float2(x, y) * pixelSize, 0).r;
			minz1 = min(minz1, depth1);
			maxz1 = max(maxz1, depth1);
			float4 wPos1 = mul(float4(posX1.xy / posX1.w, depth1, 1), g_mProjToWorld1);
			wPos1 /= wPos1.w;


			if (distance(wPos.xyz, wPos1.xyz) < 0.1)
			{
				aa = true;
			}
		}
	float mid1 = (minz1 + maxz1) / 2;
	if (mid1> maxz|| mid1 <minz)
	{
		aa = false;
	}
	if (aa)
	{
		color += _previousResult.SampleLevel(s0, uv2, 0).rgb;
		weight += 1;
	}
	color /= weight;
#if DEBUG_TAA
	if (weight == 1)
		return float4(0.75, 0.5, 0.75, 1);
#endif

#endif
	color = max(0.001, color);
	color = pow(color.rgb, 1 / 2.2);
	return float4(color, sourceColor.a);
}