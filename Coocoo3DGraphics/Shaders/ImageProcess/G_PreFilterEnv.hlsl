#include "../RandomNumberGenerator.hlsli"
cbuffer cb0 : register(b0)
{
	uint2 notuse0;
	int quality;
	uint batch;
	uint2 imageSize;
}
cbuffer cb1 : register(b1)
{
	uint3 notUse1;
	uint batch1;
	uint2 imageSize1;
}
const static float4x4 _xproj =
{ 0,0,-1,0,
0,-1,0,0,
0,0,0,-100,
1,0,0,100, };
const static float4x4 _nxproj =
{ 0,0,1,0,
0,-1,0,0,
0,0,0,-100,
-1,0,0,100, };
const static float4x4 _yproj =
{ 1,0,0,0,
0,0,1,0,
0,0,0,-100,
0,1,0,100, };
const static float4x4 _nyproj =
{ 1,0,0,0,
0,0,-1,0,
0,0,0,-100,
0,-1,0,100, };
const static float4x4 _zproj =
{ 1,0,0,0,
0,-1,0,0,
0,0,0,-100,
0,0,1,100, };
const static float4x4 _nzproj =
{ -1,0,0,0,
0,-1,0,0,
0,0,0,-100,
0,0,-1,100, };

const static float COO_PI = 3.141592653589793238;
RWTexture2DArray<float4> EnvMap : register(u0);
TextureCube AmbientCubemap : register(t0);
SamplerState s0 : register(s0);
float4 Pow4(float4 x)
{
	return x * x * x * x;
}
float3 Pow4(float3 x)
{
	return x * x * x * x;
}
float2 Pow4(float2 x)
{
	return x * x * x * x;
}
float Pow4(float x)
{
	return x * x * x * x;
}
float4 Pow2(float4 x)
{
	return x * x;
}
float3 Pow2(float3 x)
{
	return x * x;
}
float2 Pow2(float2 x)
{
	return x * x;
}
float Pow2(float x)
{
	return x * x;
}
float4 ImportanceSampleGGX(float2 E, float a2)
{
	float Phi = 2 * COO_PI * E.x;
	float CosTheta = sqrt((1 - E.y) / (1 + (a2 - 1) * E.y));
	float SinTheta = sqrt(1 - CosTheta * CosTheta);

	float3 H;
	H.x = SinTheta * cos(Phi);
	H.y = SinTheta * sin(Phi);
	H.z = CosTheta;

	float d = (CosTheta * a2 - CosTheta) * CosTheta + 1;
	float D = a2 / (COO_PI * d * d);
	float PDF = D * CosTheta;

	return float4(H, PDF);
}

float3x3 GetTangentBasis(float3 TangentZ)
{
	const float Sign = TangentZ.z >= 0 ? 1 : -1;
	const float a = -rcp(Sign + TangentZ.z);
	const float b = TangentZ.x * TangentZ.y * a;

	float3 TangentX = { 1 + Sign * a * Pow2(TangentZ.x), Sign * b, -Sign * TangentZ.x };
	float3 TangentY = { b,  Sign + a * Pow2(TangentZ.y), -TangentZ.y };

	return float3x3(TangentX, TangentY, TangentZ);
}

float3 TangentToWorld(float3 Vec, float3 TangentZ)
{
	return mul(Vec, GetTangentBasis(TangentZ));
}

float3 PrefilterEnvMap(uint2 Random, float Roughness, float3 R)
{
	float3 FilteredColor = 0;
	float Weight = 0;

	uint NumSamples = 128;
	if (Roughness < 0.5)
		NumSamples = 32;
	if (Roughness < 0.25)
		NumSamples = 16;
	for (uint i = 0; i < NumSamples; i++)
	{
		float2 E = RNG::Hammersley(i, NumSamples, Random);
		float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(Roughness)).xyz, R);
		float3 L = 2 * dot(R, H) * H - R;

		float NoL = saturate(dot(R, L));
		if (NoL > 0)
		{
			FilteredColor += AmbientCubemap.SampleLevel(s0, L, clamp(round(Roughness * 4), 0, 4)).rgb * NoL;
			//FilteredColor += float4(1,1,1,1).rgb * NoL;
			Weight += NoL;
		}
	}

	return FilteredColor / max(Weight, 0.001);
}



[numthreads(8, 8, 1)]
void main(uint3 dtid : SV_DispatchThreadID)
{
	float3 N = float4(0, 0, 0, 0);
	uint ba1 = (int)pow(2, batch1 + 1) / 2;
	uint2 size1 = imageSize / ba1;
	uint randomState = RNG::RandomSeed(dtid.x + dtid.y * 2048 + dtid.z * 4194304 + batch * 67108864);
	float2 screenPos = ((float2)dtid.xy + 0.5f) / (float2)size1 * 2 - 1;
	if (dtid.x > size1.x || dtid.y > size1.y)
	{
		return;
	}
	if (dtid.z == 0)
	{
		N = mul(float4(screenPos, 0, 1), _xproj);
	}
	else if (dtid.z == 1)
	{
		N = mul(float4(screenPos, 0, 1), _nxproj);
	}
	else if (dtid.z == 2)
	{
		N = mul(float4(screenPos, 0, 1), _yproj);
	}
	else if (dtid.z == 3)
	{
		N = mul(float4(screenPos, 0, 1), _nyproj);
	}
	else if (dtid.z == 4)
	{
		N = mul(float4(screenPos, 0, 1), _zproj);
	}
	else
	{
		N = mul(float4(screenPos, 0, 1), _nzproj);
	}
	N = normalize(N);
	if (batch1 == 0)
		EnvMap[dtid] = float4(PrefilterEnvMap(uint2(RNG::Random(randomState), RNG::Random(randomState)), 0, N) / quality + EnvMap[dtid].rgb, 1);
	else
		EnvMap[dtid] = float4(PrefilterEnvMap(uint2(RNG::Random(randomState), RNG::Random(randomState)), Pow2(batch1 / 6.0f), N) / quality + EnvMap[dtid].rgb, 1);
}