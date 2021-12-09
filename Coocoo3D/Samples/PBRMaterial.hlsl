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
	float _Emission;
	float _Specular;
	float4 _DiffuseColor;
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
Texture2D Emission : register(t1);
Texture2D ShadowMap0 : register(t2);
TextureCube EnvCube : register (t3);
Texture2D BRDFLut : register(t4);

#define MAX_BONE_MATRICES 1024
cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};

struct VSSkinnedIn
{
	float3 Pos	: POSITION0;		//Position
	float4 Weights : WEIGHTS;		//Bone weights
	uint4  Bones : BONES;			//Bone indices
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tan : TANGENT;		    //Normalized Tangent vector
};


struct SkinnedInfo
{
	float4 Pos;
	float3 Norm;
	float3 Tan;
};

matrix FetchBoneTransform(uint iBone)
{
	matrix mret;
	mret = g_mConstBoneWorld[iBone];
	return mret;
}

SkinnedInfo SkinVert(VSSkinnedIn Input)
{
	SkinnedInfo Output = (SkinnedInfo)0;

	float4 Pos = float4(Input.Pos, 1);
	float3 Norm = Input.Norm;
	float3 Tan = Input.Tan;

	//Bone0
	uint iBone = Input.Bones.x;
	float fWeight = Input.Weights.x;
	matrix m;
	if (iBone < MAX_BONE_MATRICES)
	{
		m = FetchBoneTransform(iBone);
		Output.Pos += fWeight * mul(Pos, m);
		Output.Norm += fWeight * mul(float4(Norm, 0), m).xyz;
		Output.Tan += fWeight * mul(float4(Tan, 0), m).xyz;
	}
	//Bone1
	iBone = Input.Bones.y;
	fWeight = Input.Weights.y;
	if (iBone < MAX_BONE_MATRICES)
	{
		m = FetchBoneTransform(iBone);
		Output.Pos += fWeight * mul(Pos, m);
		Output.Norm += fWeight * mul(float4(Norm, 0), m).xyz;
		Output.Tan += fWeight * mul(float4(Tan, 0), m).xyz;
	}
	//Bone2
	iBone = Input.Bones.z;
	fWeight = Input.Weights.z;
	if (iBone < MAX_BONE_MATRICES)
	{
		m = FetchBoneTransform(iBone);
		Output.Pos += fWeight * mul(Pos, m);
		Output.Norm += fWeight * mul(float4(Norm, 0), m).xyz;
		Output.Tan += fWeight * mul(float4(Tan, 0), m).xyz;
	}
	//Bone3
	iBone = Input.Bones.w;
	fWeight = Input.Weights.w;
	if (iBone < MAX_BONE_MATRICES)
	{
		m = FetchBoneTransform(iBone);
		Output.Pos += fWeight * mul(Pos, m);
		Output.Norm += fWeight * mul(float4(Norm, 0), m).xyz;
		Output.Tan += fWeight * mul(float4(Tan, 0), m).xyz;
	}
	return Output;
}

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

	SkinnedInfo vSkinned = SkinVert(input);
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
	float4 texColor = texture0.Sample(s1, input.TexCoord) * _DiffuseColor;
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

	float3 emission = Emission.Sample(s1, input.TexCoord) * _Emission;

#if ENABLE_DIFFUSE
	outputColor += EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple * c_diffuse;
#endif
#if ENABLE_SPECULR
	outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness,1e-5)) * 4) * g_skyBoxMultiple * GF;
#endif
	outputColor += emission;
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
#if DEBUG_EMISSION
	return float4(emission, 1);
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