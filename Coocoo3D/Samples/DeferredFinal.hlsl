#include "PBR.hlsli"

struct LightInfo
{
	float3 LightDir;
	uint LightType;
	float3 LightColor;
	float useless;
};
struct PointLightInfo
{
	float3 LightDir;
	uint LightType;
	float3 LightColor;
	float useless;
};
#define POINT_LIGHT_COUNT 4
cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_camPos;
	float g_skyBoxMultiple;
	float3 _fogColor;
	float _fogDensity;
	float _startDistance;
	float _endDistance;
	int2 _widthHeight;
	int _volumeLightIterCount;
	float _volumeLightMaxDistance;
	float _volumeLightIntensity;
	float4x4 LightMapVP;
	float4x4 LightMapVP1;
	LightInfo Lightings[1];
#if ENABLE_POINT_LIGHT
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
#endif
};
Texture2D gbuffer0 :register(t0);
Texture2D gbuffer1 :register(t1);
Texture2D gbuffer2 :register(t2);
Texture2D gbuffer3 :register(t3);
TextureCube EnvCube : register (t4);
Texture2D gbufferDepth : register (t5);
Texture2D ShadowMap0 : register(t6);
TextureCube SkyBox : register (t7);
Texture2D BRDFLut : register(t8);
SamplerState s0 : register(s0);
SamplerComparisonState sampleShadowMap0 : register(s2);
SamplerState s3 : register(s3);
float3 NormalDecode(float2 enc)
{
	float4 nn = float4(enc * 2, 0, 0) + float4(-1, -1, 1, -1);
	float l = dot(nn.xyz, -nn.xyw);
	nn.z = l;
	nn.xy *= sqrt(max(l, 1e-6));
	return nn.xyz * 2 + float3(0, 0, -1);
}

struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float2 uv	: TEXCOORD;
};
struct VSIn
{
	float4 Pos	: POSITION;			//Position
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
#define ENABLE_EMISSIVE 1
#define ENABLE_DIFFUSE 1
#define ENABLE_SPECULR 1

#ifdef DEBUG_SPECULAR_RENDER
#undef ENABLE_DIFFUSE
#undef ENABLE_EMISSIVE
#endif

#ifdef DEBUG_DIFFUSE_RENDER
#undef ENABLE_SPECULR
#undef ENABLE_EMISSIVE
#endif

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.uv * 0.5 + 0.5;
	uv.y = 1 - uv.y;

	float depth1 = gbufferDepth.SampleLevel(s3, uv, 0).r;
	float4 buffer0Color = gbuffer0.SampleLevel(s3, uv, 0);
	float4 buffer1Color = gbuffer1.SampleLevel(s3, uv, 0);
	float4 buffer2Color = gbuffer2.SampleLevel(s3, uv, 0);
	//float4 buffer3Color = gbuffer3.SampleLevel(s3, uv, 0);


	float4 test1 = mul(float4(input.uv, depth1, 1), g_mProjToWorld);
	test1 /= test1.w;
	float4 wPos = float4(test1.xyz, 1);

	float3 V = normalize(g_camPos - wPos);

	float3 cam2Surf = g_camPos - wPos;
	float camDist = length(cam2Surf);

	float3 outputColor = float3(0, 0, 0);
	if (depth1 != 1.0)
	{
		float3 N = normalize(NormalDecode(buffer1Color.rg));
		float NdotV = saturate(dot(N, V));
		float roughness = buffer1Color.b;
		float alpha = roughness * roughness;
		float3 c_diffuse = buffer0Color.rgb;
		float3 c_specular = buffer2Color.rgb;
		float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
		float3 GF = c_specular * AB.x + AB.y;
		//float3 emissive = buffer3Color.rgb;
		float3 emissive = float3(buffer0Color.a, buffer1Color.a, buffer2Color.a) * 16;

#if ENABLE_DIFFUSE
		outputColor += EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple * c_diffuse;
#endif
#if ENABLE_SPECULR
#ifndef RAY_TRACING
		outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF;
#endif
#endif
#ifdef ENABLE_EMISSIVE
		outputColor += emissive;
#endif

#if DEBUG_DEPTH
		float _depth1 = pow(depth1,2.2f);
		if (_depth1 < 1)
			return float4(_depth1, _depth1, _depth1, 1);
		else
			return float4(1, 0, 0, 1);
#endif
#if DEBUG_DIFFUSE
		return float4(c_diffuse, 1);
#endif
#if DEBUG_EMISSIVE
		return float4(emissive, 1);
#endif
#if DEBUG_NORMAL
		return float4(pow(N * 0.5 + 0.5, 2.2f), 1);
#endif
#if DEBUG_POSITION
		return wPos;
#endif
#if DEBUG_ROUGHNESS
		float _roughness1 = pow(max(roughness, 0.0001f), 2.2f);
		return float4(_roughness1, _roughness1, _roughness1, 1);
#endif
#if DEBUG_SPECULAR
		return float4(c_specular, 1);
#endif
#if ENABLE_DIRECTIONAL_LIGHT
		for (int i = 0; i < 1; i++)
		{
			if (!any(Lightings[i].LightColor))continue;
			if (Lightings[i].LightType == 0)
			{
				float inShadow = 1.0f;
				float3 lightStrength = max(Lightings[i].LightColor.rgb, 0);
				if (i == 0)
				{
					float4 sPos;
					float2 shadowTexCoords;
					sPos = mul(wPos, LightMapVP);
					sPos = sPos / sPos.w;
					shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
					if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
						inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 1), sPos.z).r;
					else
					{
						sPos = mul(wPos, LightMapVP1);
						sPos = sPos / sPos.w;
						shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
						shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
						if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
							inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 1) + float2(0.5, 0), sPos.z).r;
					}
				}

				float3 L = normalize(Lightings[i].LightDir);
				float3 H = normalize(L + V);

				float3 NdotL = saturate(dot(N, L));
				float3 LdotH = saturate(dot(L, H));
				float3 NdotH = saturate(dot(N, H));

#if ENABLE_DIFFUSE
			float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
#else
			float diffuse_factor = 0;
#endif
#if ENABLE_SPECULR
			float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);
#else
			float3 specular_factor = 0;
#endif

				outputColor += NdotL * lightStrength * ((c_diffuse * diffuse_factor / COO_PI) + specular_factor) * inShadow;
			}
		}
#endif//ENABLE_DIRECTIONAL_LIGHT
#if ENABLE_POINT_LIGHT
		for (int i = 0; i < POINT_LIGHT_COUNT; i++)
		{
			if (PointLights[i].LightType == 1)
			{
				float inShadow = 1.0f;
				float3 lightStrength = PointLights[i].LightColor.rgb / pow(distance(PointLights[i].LightDir, wPos), 2);

				float3 L = normalize(PointLights[i].LightDir - wPos);
				float3 H = normalize(L + V);

				float3 NdotL = saturate(dot(N, L));
				float3 LdotH = saturate(dot(L, H));
				float3 NdotH = saturate(dot(N, H));

#if ENABLE_DIFFUSE
				float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
#else
				float diffuse_factor = 0;
#endif
#if ENABLE_SPECULR
				float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);
#else
				float3 specular_factor = 0;
#endif

				outputColor += NdotL * lightStrength * ((c_diffuse * diffuse_factor / COO_PI) + specular_factor) * inShadow;
			}
		}
#endif//ENABLE_POINT_LIGHT
#if ENABLE_FOG
		outputColor = lerp(_fogColor, outputColor, 1 / exp(max(camDist - _startDistance, 0.00001) * _fogDensity));
#endif
	}
	else
	{
		outputColor = SkyBox.Sample(s0, -V).rgb * g_skyBoxMultiple;
	}
#if ENABLE_DIRECTIONAL_LIGHT
#if ENABLE_VOLUME_LIGHTING
		int volumeLightIterCount = _volumeLightIterCount;
		float volumeLightMaxDistance = _volumeLightMaxDistance;
		float volumeLightIntensity = _volumeLightIntensity;

		for (int i = 0; i < 1; i++)
		{
			if (!any(Lightings[i].LightColor))continue;
			if (Lightings[i].LightType == 0)
			{
				float3 lightStrength = max(Lightings[i].LightColor.rgb, 0);
				float volumeLightIterStep = volumeLightMaxDistance / volumeLightIterCount;
				volumeLightIterStep /= sqrt(clamp(1 - pow2(dot(Lightings[i].LightDir, -V)), 0.04, 1));
				float offset = ((uv.x * _widthHeight.x * 9323 + (uv.y * _widthHeight.y - 0.5f) * 10501) % 34739 / 34739) * volumeLightIterStep;
				float3 samplePos1 = g_camPos;
				for (int j = 0; j < volumeLightIterCount; j++)
				{
					if (j * volumeLightIterStep + offset > camDist)
					{
						break;
					}
					float inShadow = 1.0f;
					float4 samplePos = float4(g_camPos - V * (volumeLightIterStep * j + offset), 1);
					float4 sPos;
					float2 shadowTexCoords;
					sPos = mul(samplePos, LightMapVP);
					sPos = sPos / sPos.w;
					shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
					if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
						inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 1), sPos.z).r;
					else
					{
						sPos = mul(samplePos, LightMapVP1);
						sPos = sPos / sPos.w;
						shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
						shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
						if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
							inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 1) + float2(0.5, 0), sPos.z).r;
					}

					outputColor += inShadow * lightStrength * volumeLightIterStep * volumeLightIntensity;
				}
			}
		}
#endif
#endif
	return float4(outputColor, 1);
}