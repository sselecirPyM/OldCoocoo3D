#ifdef SKINNING
#define MAX_BONE_MATRICES 1024
#endif

#include "Skinning.hlsli"
#include "PBR.hlsli"
#include "SH.hlsli"

struct LightInfo
{
	float3 LightDir;
	uint LightType;
	float3 LightColor;
	float useless;
};
struct PointLightInfo
{
	float3 LightPos;
	uint LightType;
	float3 LightColor;
	float LightRange;
};
#define POINT_LIGHT_COUNT 4
cbuffer cb1 : register(b1)
{
	float4x4 g_mWorld;
#if ENABLE_POINT_LIGHT
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
#endif
	float _Metallic;
	float _Roughness;
	float _Emissive;
	float _Specular;
}

cbuffer cb2 : register(b2)
{
	float4x4 g_mWorldToProj;
	float3 g_camPos;
	float4x4 LightMapVP;
	float4x4 LightMapVP1;
	LightInfo Lightings[1];
	float g_skyBoxMultiple;
	float3 _fogColor;
	float _fogDensity;
	float _startDistance;
	float _endDistance;
	int  g_lightMapSplit;
	float3 g_GIVolumePosition;
	float3 g_GIVolumeSize;
}
#define SH_RESOLUTION (16)
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
SamplerState s3 : register(s3);
Texture2D texture0 : register(t0);
Texture2D Emissive : register(t1);
Texture2D ShadowMap0 : register(t2);
TextureCube EnvCube : register (t3);
Texture2D BRDFLut : register(t4);
Texture2D NormalMap : register(t5);
StructuredBuffer<SH9C> giBuffer : register(t6);
cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		//Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
	float3 Bitangent : BITANGENT;
};

PSSkinnedIn vsmain(VSSkinnedIn input)
{
	PSSkinnedIn output;
	SkinnedInfo vSkinned = SkinVert(input, g_mConstBoneWorld);
	float3 pos = mul(vSkinned.Pos, g_mWorld);
	output.Norm = normalize(mul(vSkinned.Norm, (float3x3)g_mWorld));
	output.Tangent = normalize(mul(vSkinned.Tan, (float3x3)g_mWorld));
	output.Bitangent = cross(output.Norm, output.Tangent) * input.Tan.w;
	output.Tex = input.Tex;

	output.Pos = mul(float4(pos, 1), g_mWorldToProj);
	output.wPos = float4(pos, 1);

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

float getDepth(float z, float near, float far)
{
	return (far - (far / z) * near) / (far - near);
}

float4 psmain(PSSkinnedIn input) : SV_TARGET
{
	float4 texColor = texture0.Sample(s1, input.Tex);
	clip(texColor.a - 0.01f);

	float4 wPos = input.wPos;
	float3 cam2Surf = g_camPos - wPos;
	float camDist = length(cam2Surf);
	float3 V = normalize(cam2Surf);
#if !USE_NORMAL_MAP
	float3 N = normalize(input.Norm);
#else
	float3x3 tbn = float3x3(normalize(input.Tangent), normalize(input.Bitangent), normalize(input.Norm));
	float3 dn = NormalMap.Sample(s1, input.Tex) * 2 - 1;
	float3 N = normalize(mul(dn, tbn));
#endif
	float NdotV = saturate(dot(N, V));
	// Burley roughness bias
	float roughness = max(_Roughness,0.002);
	float alpha = roughness * roughness;

	float3 albedo = texColor.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic);

	float3 outputColor = float3(0,0,0);
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	float3 emissive = Emissive.Sample(s1, input.Tex) * _Emissive;

#if ENABLE_DIFFUSE
	float3 skyLight = EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple;
#ifndef ENABLE_GI
	outputColor += skyLight * c_diffuse;
#else
	float3 giSamplePos = (((wPos.xyz - g_GIVolumePosition) / g_GIVolumeSize) + 0.5f);
	int3 samplePos1 = floor(SH_RESOLUTION * giSamplePos);
	float weights = 0;

	float3 lp = frac(SH_RESOLUTION * giSamplePos);
	float4 shDiffuse = float4(0, 0, 0, 0);
	for (int i = 0; i < 2; i++)
		for (int j = 0; j < 2; j++)
			for (int k = 0; k < 2; k++)
			{
				int3 _xyz = int3(i, j, k);
				int3 xyz = _xyz + samplePos1;
				float3 p1 = abs(lp - (1 - _xyz));
				float weight = p1.x * p1.y * p1.z;
				//float weight = 1;
				if (dot(N, _xyz - 0.5) >= -1e2)
				{
					if (xyz.x < SH_RESOLUTION && xyz.y < SH_RESOLUTION && xyz.z < SH_RESOLUTION && xyz.x >= 0 & xyz.y >= 0 && xyz.z >= 0)
					{
						int index = xyz.x + xyz.y * SH_RESOLUTION + xyz.z * SH_RESOLUTION * SH_RESOLUTION;
						SH9C sh9ca = giBuffer[index];
						shDiffuse += GetSH9Color(sh9ca, N) * weight;
						weights += weight;
					}
					else
					{
						shDiffuse += float4(skyLight * weight, 0);
						weights += weight;
					}
				}
			}
	weights = max(weights, 1e-3);
	shDiffuse /= weights;

	outputColor += shDiffuse.rgb * c_diffuse;
#endif
#endif
#if ENABLE_SPECULR
	outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness,1e-5)) * 4) * g_skyBoxMultiple * GF;
#endif
#if ENABLE_EMISSIVE
	outputColor += emissive;
#endif
#if DEBUG_ALBEDO
	return float4(albedo, 1);
#endif
#if DEBUG_DEPTH
	float _depth1 = pow(input.Pos.z,2.2f);
	if (_depth1 < 1)
		return float4(_depth1, _depth1, _depth1,1);
	else
		return float4(1, 0, 0, 1);
#endif
#if DEBUG_DIFFUSE
	return float4(c_diffuse,1);
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
	float _roughness1 = pow(max(roughness,0.0001f), 2.2f);
	return float4(_roughness1, _roughness1, _roughness1,1);
#endif
#if DEBUG_SPECULAR
	return float4(c_specular,1);
#endif
#if DEBUG_UV
	return float4(input.Tex,0,1);
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
#ifndef DISBLE_SHADOW_RECEIVE
				float4 sPos;
				float2 shadowTexCoords;
				sPos = mul(wPos, LightMapVP);
				sPos = sPos / sPos.w;
				shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
				shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
				if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
					inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5 ,0.5), sPos.z).r;
				else
				{
					sPos = mul(wPos, LightMapVP1);
					sPos = sPos / sPos.w;
					shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
					if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
						inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords * float2(0.5, 0.5) + float2(0.5, 0), sPos.z).r;
				}
#endif
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

			outputColor += NdotL * lightStrength * (((c_diffuse * diffuse_factor / COO_PI) + specular_factor)) * inShadow;
		}
	}
#endif //ENABLE_DIRECTIONAL_LIGHT
#if ENABLE_POINT_LIGHT
	int shadowmapIndex = 0;
	for (int i = 0; i < POINT_LIGHT_COUNT; i++, shadowmapIndex += 6)
	{
		if (PointLights[i].LightType == 1)
		{
			float inShadow = 1.0f;
			float lightDistance = distance(PointLights[i].LightPos, wPos);
			float lightRange = PointLights[i].LightRange;
			if (lightRange < lightDistance)
				continue;
			float3 lightStrength = PointLights[i].LightColor.rgb / pow(lightDistance, 2);

			float3 vl = PointLights[i].LightPos - wPos;

			float3 L = normalize(vl);
			float3 H = normalize(L + V);

			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));

			float3 absL = abs(L);


			if (absL.x > absL.y && absL.x > absL.z)
			{
				float2 samplePos = L.zy / L.x * 0.5 + 0.5;
				int mapindex = shadowmapIndex;
				if (L.x < 0)
					samplePos.x = 1 - samplePos.x;
				if (L.x > 0)
					mapindex++;
				float _x = (float)(mapindex % g_lightMapSplit) / (float)g_lightMapSplit;
				float _y = (float)(mapindex / g_lightMapSplit) / (float)g_lightMapSplit;
				float shadowDepth = ShadowMap0.SampleLevel(s3, samplePos / g_lightMapSplit + float2(_x, _y + 0.5), 0);
				inShadow = (shadowDepth) > getDepth(abs(vl.x), lightRange * 0.001f, lightRange) ? 1 : 0;
			}
			else if (absL.y > absL.z)
			{
				float2 samplePos = L.xz / L.y * 0.5 + 0.5;
				int mapindex = shadowmapIndex + 2;
				if (L.y < 0)
					samplePos.x = 1 - samplePos.x;
				if (L.y > 0)
					mapindex++;
				float _x = (float)(mapindex % g_lightMapSplit) / (float)g_lightMapSplit;
				float _y = (float)(mapindex / g_lightMapSplit) / (float)g_lightMapSplit;
				float shadowDepth = ShadowMap0.SampleLevel(s3, samplePos / g_lightMapSplit + float2(_x, _y + 0.5), 0);
				inShadow = (shadowDepth) > getDepth(abs(vl.y), lightRange * 0.001f, lightRange) ? 1 : 0;
			}
			else
			{
				float2 samplePos = L.yx / L.z * 0.5 + 0.5;
				int mapindex = shadowmapIndex + 4;
				if (L.z < 0)
					samplePos.x = 1 - samplePos.x;
				if (L.z > 0)
					mapindex++;
				float _x = (float)(mapindex % g_lightMapSplit) / (float)g_lightMapSplit;
				float _y = (float)(mapindex / g_lightMapSplit) / (float)g_lightMapSplit;
				if (samplePos.x < 0 || samplePos.x>1 || samplePos.y < 0 || samplePos.y>1)
					return float4(1, 0, 1, 1);
				float shadowDepth = ShadowMap0.SampleLevel(s3, samplePos / g_lightMapSplit + float2(_x, _y + 0.5), 0);
				inShadow = (shadowDepth) > getDepth(abs(vl.z), lightRange * 0.001f, lightRange) ? 1 : 0;
			}

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
#endif //ENABLE_POINT_LIGHT
#if ENABLE_FOG
	outputColor = lerp(pow(max(_fogColor , 1e-6),2.2f), outputColor,1 / exp(max((camDist - _startDistance) / 10,0.00001) * _fogDensity));
#endif
	return float4(outputColor, texColor.a);
}