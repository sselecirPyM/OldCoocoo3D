#ifdef SKINNING
#define MAX_BONE_MATRICES 1024
#endif

#include "Skinning.hlsli"
#include "PBR.hlsli"

struct LightInfo
{
	float4x4 LightMapVP;
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
cbuffer cb1 : register(b1)
{
	float4x4 g_mWorld;
	float4x4 g_mWorldToProj;
	LightInfo Lightings[1];
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
	float _Metallic;
	float _Roughness;
	float _Emissive;
	float _Specular;
	float3 g_camPos;
	float g_skyBoxMultiple;
	float3 _fogColor;
	float _fogDensity;
	float _startDistance;
	float _endDistance;
	float2 _notuse123123;
}
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
Texture2D texture0 : register(t0);
Texture2D Emissive : register(t1);
Texture2D ShadowMap0 : register(t2);
TextureCube EnvCube : register (t3);
Texture2D BRDFLut : register(t4);
cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 TexCoord	: TEXCOORD;		//Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
};

PSSkinnedIn vsmain(VSSkinnedIn input)
{
	PSSkinnedIn output;
	SkinnedInfo vSkinned = SkinVert(input, g_mConstBoneWorld);
	float3 pos = mul(vSkinned.Pos, g_mWorld);
	output.Norm = normalize(mul(vSkinned.Norm, (float3x3)g_mWorld));
	output.Tangent = normalize(mul(vSkinned.Tan, (float3x3)g_mWorld));
	output.TexCoord = input.Tex;

	output.Pos = mul(float4(pos, 1), g_mWorldToProj);
	output.wPos = float4(pos, 1);

	return output;
}
#define ENABLE_DIFFUSE 1
#define ENABLE_SPECULR 1

#ifdef DEBUG_SPECULAR_RENDER
#undef ENABLE_DIFFUSE
#endif

#ifdef DEBUG_DIFFUSE_RENDER
#undef ENABLE_SPECULR
#endif

float4 psmain(PSSkinnedIn input) : SV_TARGET
{
	float4 texColor = texture0.Sample(s1, input.TexCoord);
	clip(texColor.a - 0.01f);

	float3 cam2Surf = g_camPos - input.wPos;
	float camDist = length(cam2Surf);
	float3 V = normalize(cam2Surf);
	float3 N = normalize(input.Norm);
	float NdotV = saturate(dot(N, V));

	// Burley roughness bias
	float roughness = max(_Roughness,0.002);
	float alpha = roughness * roughness;

	float3 albedo = texColor.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic);

	float3 outputColor = float3(0,0,0);
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	float3 emissive = Emissive.Sample(s1, input.TexCoord) * _Emissive;

#if ENABLE_DIFFUSE
	outputColor += EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple * c_diffuse;
#endif
#if ENABLE_SPECULR
	outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness,1e-5)) * 4) * g_skyBoxMultiple * GF;
#endif
	outputColor += emissive;
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
	return input.wPos;
#endif
#if DEBUG_ROUGHNESS
	float _roughness1 = pow(max(roughness,0.0001f), 2.2f);
	return float4(_roughness1, _roughness1, _roughness1,1);
#endif
#if DEBUG_SPECULAR
	return float4(c_specular,1);
#endif
#if DEBUG_UV
	return float4(input.TexCoord,0,1);
#endif
#if ENABLE_LIGHT
	for (int i = 0; i < 1; i++)
	{
		if (!any(Lightings[i].LightColor))continue;
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			float3 lightStrength = max(Lightings[i].LightColor.rgb, 0);
			float4 sPos = mul(input.wPos, Lightings[i].LightMapVP);
			sPos = sPos / sPos.w;

			float2 shadowTexCoords;
			shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
			shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
#ifndef DISBLE_SHADOW_RECEIVE
			if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
				inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords, sPos.z).r;
#endif
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
	for (int i = 0; i < 4; i++)
	{
		if (PointLights[i].LightType == 1)
		{
			float inShadow = 1.0f;
			float3 lightStrength = PointLights[i].LightColor.rgb / pow(distance(PointLights[i].LightDir, input.wPos), 2);

			float3 L = normalize(PointLights[i].LightDir - input.wPos);
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
#endif //ENABLE_LIGHT
#if ENABLE_FOG
	outputColor = lerp(_fogColor, outputColor,1 / exp(max(camDist - _startDistance,0.00001) * _fogDensity));
#endif
	return float4(outputColor, texColor.a);
}