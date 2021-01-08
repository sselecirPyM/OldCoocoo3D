Texture2D texture0 :register(t0);
SamplerState s0 : register(s0);
Texture2D texture1 :register(t1);

cbuffer cb0 : register(b0)
{
	float4x4 g_cameraMatrix;
	float4x4 g_vTest;
}

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv : TEXCOORD0;
	float4 otherInfo : TEXCOORD1;
};

float luminance(float3 rgb)
{
	return dot(rgb, float3(0.299f, 0.587f, 0.114f));
}

float4 main(PSIn input) : SV_TARGET
{
	float4 color = texture0.Sample(s0, input.uv);
	float2 texcoord;
	texcoord.x = input.otherInfo.x * 0.5 + 0.5;
	texcoord.y = -input.otherInfo.y * 0.5 + 0.5;
	float depth1 = texture1.SampleLevel(s0, texcoord, 0).r;
	float4 test1 = mul(float4(input.otherInfo.xy, depth1, 1), g_vTest);
	test1 /= test1.w;
	float4 test2 = mul(float4(input.otherInfo.xy, input.otherInfo.w, 1), g_vTest);
	test2 /= test2.w;
	float distance1 = distance(test1.xyz, test2.xyz);

	if (depth1 < input.otherInfo.w && distance1 > 1)
	{
		color.a *= 0.2;
		color.rgb = luminance(color.rgb);
	}
	return color;
}