struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv : TEXCOORD;
};

float luminance(float3 rgb)
{
	return dot(rgb, float3(0.299f, 0.587f, 0.114f));
}

float3 TonemapACES(float3 x)
{
	const float A = 2.51f;
	const float B = 0.03f;
	const float C = 2.43f;
	const float D = 0.59f;
	const float E = 0.14f;
	return (x * (A * x + B)) / (x * (C * x + D) + E);
}

float3 TonemapHable(float3 x)
{
	const float A = 0.22;
	const float B = 0.30;
	const float C = 0.10;
	const float D = 0.20;
	const float E = 0.01;
	const float F = 0.30;
	return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

Texture2D texture0 :register(t0);
Texture2D background :register(t1);
SamplerState s0 : register(s0);
cbuffer cb0 : register(b0)
{
	float _GammaCorrection;
	float _ColorSaturation1;
	float _Threshold1;
	float _Transition1;
	float _ColorSaturation2;
	float _Threshold2;
	float _Transition2;
	float _ColorSaturation3;
	float _BackgroundFactory;
};
float4 main(PSSkinnedIn input) : SV_TARGET
{
	float4 sourceColor = texture0.Sample(s0, input.uv);
	float3 color = sourceColor.rgb;
	float3 lum = luminance(color);
	float3 color1 = lerp(lum, color, _ColorSaturation1);
	float3 color2 = lerp(lum, color, _ColorSaturation2);
	float3 color3 = lerp(lum, color, _ColorSaturation3);
	color = lerp(color3, lerp(color2, color1, smoothstep(_Threshold1 - _Transition1, _Threshold1, lum)), smoothstep(_Threshold2 - _Transition2, _Threshold2, lum));
	color = max(0, color);
	float4 backgroundColor = background.Sample(s0, input.uv);
	color = color.rgb * (1 - backgroundColor.a * _BackgroundFactory) + backgroundColor.rgb * backgroundColor.a * _BackgroundFactory;
	color = pow(color.rgb, 1 / _GammaCorrection);
	return float4(color, sourceColor.a);
}