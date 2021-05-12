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
	float4x4 _worldToProj;
	LightInfo Lightings[1];
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
	float _Metallic;
	float _Roughness;
	float _Emission;
	float _Specular;
	float4 _DiffuseColor;
	float3 g_vCamPos;
	float g_skyBoxMultiple;
}
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
Texture2D texture0 : register(t0);
Texture2D texture1 : register(t1);
Texture2D ShadowMap0 : register(t2);
TextureCube EnvCube : register (t3);
TextureCube IrradianceCube : register (t4);
Texture2D BRDFLut : register(t5);
struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 TexCoord	: TEXCOORD;		//Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
};
float4 main(PSSkinnedIn input) : SV_TARGET
{
	float4 texColor = texture0.Sample(s1, input.TexCoord) * _DiffuseColor;
	clip(texColor.a - 0.01f);

	float3 V = normalize(g_vCamPos - input.wPos);
	float3 N = normalize(input.Norm);
	float NdotV = saturate(dot(N, V));

	// Burley roughness bias
	float roughness = max(_Roughness,0.002);
	float alpha = roughness * roughness;

	float3 albedo = texColor.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic);

	float3 outputColor = float3(0,0,0);
	for (int i = 0; i < 1; i++)
	{
		if (Lightings[i].LightColor.a == 0)continue;
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			float3 lightStrength = max(Lightings[i].LightColor.rgb * Lightings[i].LightColor.a, 0);
			float4 sPos = mul(input.wPos, Lightings[i].LightMapVP);
			sPos = sPos / sPos.w;

			float2 shadowTexCoords;
			shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
			shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);

			if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
				inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords, sPos.z).r;

			float3 L = normalize(Lightings[i].LightDir);
			float3 H = normalize(L + V);

			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));

			float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
			float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

			outputColor += NdotL * lightStrength * (((c_diffuse * diffuse_factor / COO_PI) + specular_factor)) * inShadow;
		}
		else if (Lightings[i].LightType == 1)
		{
			float inShadow = 1.0f;
			float3 lightStrength = Lightings[i].LightColor.rgb * Lightings[i].LightColor.a / pow(distance(Lightings[i].LightDir, input.wPos), 2);

			float3 L = normalize(Lightings[i].LightDir - input.wPos);
			float3 H = normalize(L + V);

			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));

			float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
			float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

			outputColor += NdotL * lightStrength * (((c_diffuse * diffuse_factor / COO_PI) + specular_factor)) * inShadow;
		}
	}
	for (int i = 0; i < 4; i++)
	{
		if (PointLights[i].LightType == 1)
		{
			float inShadow = 1.0f;
			float3 lightStrength = PointLights[i].LightColor.rgb * PointLights[i].LightColor.a / pow(distance(PointLights[i].LightDir, input.wPos), 2);

			float3 L = normalize(PointLights[i].LightDir - input.wPos);
			float3 H = normalize(L + V);

			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));

			float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
			float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

			outputColor += NdotL * lightStrength * (((c_diffuse * diffuse_factor / COO_PI) + specular_factor)) * inShadow;
		}
	}
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	outputColor += IrradianceCube.Sample(s0, N) * g_skyBoxMultiple * c_diffuse;
	outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness,1e-5)) * 6) * g_skyBoxMultiple * GF;
	outputColor += _Emission * albedo;
	//outputColor *= float4(1, 0.5, 0.5, 1);
	return float4(outputColor, texColor.a);
}