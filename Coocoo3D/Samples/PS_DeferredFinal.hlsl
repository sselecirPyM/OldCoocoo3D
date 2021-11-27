static const float COO_PI = 3.141592653589793238f;
static const float COO_EPSILON = 1e-5f;

float4 pow5(float4 x)
{
	return x * x * x * x * x;
}
float3 pow5(float3 x)
{
	return x * x * x * x * x;
}
float2 pow5(float2 x)
{
	return x * x * x * x * x;
}
float pow5(float x)
{
	return x * x * x * x * x;
}

float4 pow2(float4 x)
{
	return x * x;
}
float3 pow2(float3 x)
{
	return x * x;
}
float2 pow2(float2 x)
{
	return x * x;
}
float pow2(float x)
{
	return x * x;
}

// Shlick's approximation of Fresnel
// https://en.wikipedia.org/wiki/Schlick%27s_approximation
float3 Fresnel_Shlick(in float3 f0, in float3 f90, in float x)
{
	return f0 + (f90 - f0) * pow5(1.f - x);
}

// Burley B. "Physically Based Shading at Disney"
// SIGGRAPH 2012 Course: Practical Physically Based Shading in Film and Game Production, 2012.
float Diffuse_Burley(in float NdotL, in float NdotV, in float LdotH, in float roughness)
{
	float fd90 = 0.5f + 2.f * roughness * LdotH * LdotH;
	return Fresnel_Shlick(1, fd90, NdotL).x * Fresnel_Shlick(1, fd90, NdotV).x;
}

float Diffuse_Lambert(in float NdotL, in float NdotV, in float LdotH, in float roughness)
{
	return NdotL;
}

// GGX specular D (normal distribution)
// https://www.cs.cornell.edu/~srm/publications/EGSR07-btdf.pdf
float Specular_D_GGX(in float alpha, in float NdotH)
{
	const float alpha2 = alpha * alpha;
	const float lower = (NdotH * alpha2 - NdotH) * NdotH + 1;
	return alpha2 / max(COO_EPSILON, COO_PI * lower * lower);
}

// Schlick-Smith specular G (visibility) with Hable's LdotH optimization
// http://www.cs.virginia.edu/~jdl/bib/appearance/analytic%20models/schlick94b.pdf
// http://graphicrants.blogspot.se/2013/08/specular-brdf-reference.html
float G_Shlick_Smith_Hable(float alpha, float LdotH)
{
	return rcp(lerp(LdotH * LdotH, 1, alpha * alpha * 0.25f));
}

// A microfacet based BRDF.
//
// alpha:           This is roughness * roughness as in the "Disney" PBR model by Burley et al.
//
// specularColor:   The F0 reflectance value - 0.04 for non-metals, or RGB for metals. This follows model 
//                  used by Unreal Engine 4.
//
// NdotV, NdotL, LdotH, NdotH: vector relationships between,
//      N - surface normal
//      V - eye normal
//      L - light normal
//      H - half vector between L & V.
float3 Specular_BRDF(in float alpha, in float3 specularColor, in float NdotV, in float NdotL, in float LdotH, in float NdotH)
{
	float specular_D = Specular_D_GGX(alpha, NdotH);
	float3 specular_F = Fresnel_Shlick(specularColor, 1, LdotH);
	float specular_G = G_Shlick_Smith_Hable(alpha, LdotH);

	return specular_D * specular_F * specular_G;
}

float3 Fresnel_SchlickRoughness(float cosTheta, float3 F0, float roughness)
{
	return F0 + (max(1.0 - roughness, F0) - F0) * pow5(1.0 - cosTheta);
}
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
Texture2D texture2 :register(t2);
Texture2D gbufferDepth : register (t3);
TextureCube EnvCube : register (t4);
Texture2D BRDFLut : register(t5);
Texture2D ShadowMap0 : register(t6);
TextureCube SkyBox : register (t7);
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
	float4 buffer2Color = texture2.Sample(s3, uv);

	float4 vx = mul(float4(input.uv,0,1),g_mProjToWorld);
	float3 V = normalize(g_vCamPos - vx.xyz / vx.w);

	float4 test1 = mul(float4(input.uv, depth1, 1), g_mProjToWorld);
	test1 /= test1.w;
	float4 wPos = float4(test1.xyz, 1);

	if (depth1 != 1.0)
	{
		float3 N = normalize(NormalDecode(buffer1Color.rg));
		float NdotV = saturate(dot(N, V));
		float metallic = buffer0Color.a;
		float roughness = buffer1Color.b;
		float alpha = roughness * roughness;
		float3 c_diffuse = buffer0Color.rgb;
		float3 c_specular = buffer2Color.rgb;
		float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
		float3 GF = c_specular * AB.x + AB.y;
		float3 outputColor = float3(0, 0, 0);
		//outputColor += IrradianceCube.Sample(s0, N) * g_skyBoxMultiple * c_diffuse;
		outputColor += EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple * c_diffuse;
		outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF;

		for (int i = 0; i < 1; i++)
		{
			if (Lightings[i].LightColor.a == 0)continue;
			if (Lightings[i].LightType == 0)
			{
				float inShadow = 1.0f;
				float3 lightStrength = max(Lightings[i].LightColor.rgb * Lightings[i].LightColor.a, 0);
				float4 sPos = mul(wPos, Lightings[i].LightMapVP);
				sPos = sPos / sPos.w;

				float2 shadowTexCoords;
				shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
				shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
				if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
					inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, float3(shadowTexCoords, 0), sPos.z).r;

				float3 L = normalize(Lightings[i].LightDir);
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
		float3 EnvColor = SkyBox.Sample(s0, -V).rgb * g_skyBoxMultiple;
		return float4(EnvColor, 1);
	}
}