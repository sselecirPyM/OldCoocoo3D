#include "BRDF/PBR.hlsli"
struct LightInfo
{
	float4x4 LightMapVP;
	float3 LightDir;
	uint LightType;
	float4 LightColor;
};
struct PointLightInfo
{
	float3 LightDir;
	uint LightType;
	float4 LightColor;
};
#define POINT_LIGHT_COUNT 4
cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_vCamPos;
	float g_skyBoxMultiple;
	LightInfo Lightings[1];
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
};
Texture2D texture0 :register(t0);
Texture2D texture1 :register(t1);
Texture2D gbufferDepth : register (t2);

TextureCube EnvCube : register (t3);
TextureCube IrradianceCube : register (t4);
Texture2D BRDFLut : register(t5);
Texture2D ShadowMap0 : register(t6);
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
float4 main(PSIn input) : SV_TARGET
{
	float2 uv = input.uv * 0.5 + 0.5;
	uv.y = 1 - uv.y;

	float depth1 = gbufferDepth.SampleLevel(s3, uv, 0).r;
	float4 buffer0Color = texture0.Sample(s3, uv);
	float4 buffer1Color = texture1.Sample(s3, uv);

	float4 vx = mul(float4(input.uv,0,1),g_mProjToWorld);
	float3 V = normalize(g_vCamPos - vx.xyz / vx.w);

	float4 test1 = mul(float4(input.uv, depth1, 1), g_mProjToWorld);
	test1 /= test1.w;
	float4 wPos = float4(test1.xyz, 1);

	if (depth1 != 1.0)
	{
		float3 N = normalize(NormalDecode(buffer1Color.rg));
		float NdotV = saturate(dot(N, V));
		float3 albedo = buffer0Color.rgb;
		float metallic = buffer0Color.a;
		float roughness = buffer1Color.b;
		float alpha = roughness * roughness;
		float3 c_diffuse = lerp(albedo * (1 - 0.04), 0, metallic);
		float3 c_specular = lerp(0.04f, albedo, metallic);
		float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
		float3 GF = c_specular * AB.x + AB.y;
		float3 outputColor = float3(0, 0, 0);
		outputColor += IrradianceCube.Sample(s0, N) * g_skyBoxMultiple * c_diffuse;
		outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 6) * g_skyBoxMultiple * GF;

		for (int i = 0; i < 1; i++)
		{
			if (Lightings[i].LightColor.a == 0)continue;
			if (Lightings[i].LightType == 0)
			{
				float inShadow = 1.0f;
				float3 lightStrength = max(Lightings[i].LightColor.rgb * Lightings[i].LightColor.a, 0);
				//if (g_enableShadow != 0)
				//{
				float4 sPos = mul(wPos, Lightings[i].LightMapVP);
				sPos = sPos / sPos.w;

				float2 shadowTexCoords;
				shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
				shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
				if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
					inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, float3(shadowTexCoords, 0), sPos.z).r;
				//else
				//{
				//	sPos = mul(wPos, LightSpaceMatrices[1]);
				//	sPos = sPos / sPos.w;
				//	float2 shadowTexCoords1;
				//	shadowTexCoords1.x = 0.5f + (sPos.x * 0.5f);
				//	shadowTexCoords1.y = 0.5f - (sPos.y * 0.5f);

				//	if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
				//		inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, float3(shadowTexCoords1, 1), sPos.z).r;
				//}
				//if (inShadow > 0.0f)
				//{

				//}
			//}

				float3 L = normalize(Lightings[i].LightDir);
				float3 H = normalize(L + V);

				float3 NdotL = saturate(dot(N, L));
				float3 LdotH = saturate(dot(L, H));
				float3 NdotH = saturate(dot(N, H));

				float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
				float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

				outputColor += NdotL * lightStrength * ((c_diffuse * diffuse_factor / COO_PI) + specular_factor) * inShadow;
			}
			else if (Lightings[i].LightType == 1)
			{
				float inShadow = 1.0f;
				float3 lightStrength = Lightings[i].LightColor.rgb * Lightings[i].LightColor.a / pow(distance(Lightings[i].LightDir, wPos), 2);

				float3 L = normalize(Lightings[i].LightDir - wPos);
				float3 H = normalize(L + V);

				float3 NdotL = saturate(dot(N, L));
				float3 LdotH = saturate(dot(L, H));
				float3 NdotH = saturate(dot(N, H));

				float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
				float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

				outputColor += NdotL * lightStrength * ((c_diffuse * diffuse_factor / COO_PI) + specular_factor) * inShadow;
			}
		}
		for (int i = 0; i < POINT_LIGHT_COUNT; i++)
		{
			if (PointLights[i].LightType == 1)
			{
				float inShadow = 1.0f;
				float3 lightStrength = PointLights[i].LightColor.rgb * PointLights[i].LightColor.a / pow(distance(PointLights[i].LightDir, wPos), 2);

				float3 L = normalize(PointLights[i].LightDir - wPos);
				float3 H = normalize(L + V);

				float3 NdotL = saturate(dot(N, L));
				float3 LdotH = saturate(dot(L, H));
				float3 NdotH = saturate(dot(N, H));

				float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
				float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

				outputColor += NdotL * lightStrength * ((c_diffuse * diffuse_factor / COO_PI) + specular_factor) * inShadow;
			}
		}

		return float4(outputColor, 1);
	}
	else
	{
		float3 EnvColor = EnvCube.Sample(s0, -V).rgb * g_skyBoxMultiple;
		return float4(EnvColor, 0);
	}
}