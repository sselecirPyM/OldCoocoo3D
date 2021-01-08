#include "FromDisney/DisneyBRDF.hlsli"
#include "RandomNumberGenerator.hlsli"
#include "CameraDataDefine.hlsli"


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
struct LightInfo
{
	float3 LightDir;
	uint LightType;
	float4 LightColor;
};
cbuffer cb2 : register(b2)
{
	CAMERA_DATA_DEFINE;//is a macro
};
cbuffer cb1 : register(b1)
{
	float4 _DiffuseColor;
	float4 _SpecularColor;
	float3 _AmbientColor;
	float _EdgeScale;
	float4 _EdgeColor;

	float4 _Texture;
	float4 _SubTexture;
	float4 _ToonTexture;
	uint notUse;
	float _Metallic;
	float _Roughness;
	float _Emission;
	float _Subsurface;
	float _Specular;
	float _SpecularTint;
	float _Anisotropic;
	float _Sheen;
	float _SheenTint;
	float _Clearcoat;
	float _ClearcoatGloss;
	float4 materialPreserved[6];
	float4x4 LightSpaceMatrices[4];
	LightInfo Lightings[8];
};
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
Texture2D texture0 :register(t0);
Texture2D texture1 :register(t1);
Texture2DArray ShadowMap0:register(t2);
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

// Schlick-Smith specular G (visibility) with Hable's LdotH optimization
// http://www.cs.virginia.edu/~jdl/bib/appearance/analytic%20models/schlick94b.pdf
// http://graphicrants.blogspot.se/2013/08/specular-brdf-reference.html
float G_Shlick_Smith_Hable(float alpha, float LdotH)
{
	return rcp(lerp(LdotH * LdotH, 1, alpha * alpha * 0.25f));
}

float3 BRDF_1(float3 L, float3 V, float3 N, /*float3 X, float3 Y,*/
	float3 baseColor,
	float metallic = 0,
	float roughness = 0.5,
	float subsurface = 0,
	float specular = 0,
	float specularTint = 0,
	float anisotropic = 0,
	float sheen = 0,
	float sheenTint = 0.0,
	float clearcoat = 0,
	float clearcoatGloss = 0
)
{
	float NdotL = dot(N, L);
	float NdotV = dot(N, V);
	if (NdotL < 0 || NdotV < 0) return float3(0, 0, 0);

	float3 H = normalize(L + V);
	float NdotH = dot(N, H);
	float LdotH = dot(L, H);

	//float3 Cdlin = mon2lin(baseColor);
	float3 Cdlin = baseColor;
	float Cdlum = .3 * Cdlin[0] + .6 * Cdlin[1] + .1 * Cdlin[2]; // luminance approx.

	float3 Ctint = Cdlum > 0 ? Cdlin / Cdlum : float3(1, 1, 1); // normalize lum. to isolate hue+sat
	float3 Cspec0 = lerp(specular * .08 * lerp(float3(1, 1, 1), Ctint, specularTint), Cdlin, metallic);
	float3 Csheen = lerp(float3(1, 1, 1), Ctint, sheenTint);

	// Diffuse fresnel - go from 1 at normal incidence to .5 at grazing
	// and mix in diffuse retro-reflection based on roughness
	float FL = SchlickFresnel(NdotL), FV = SchlickFresnel(NdotV);
	float Fd90 = 0.5 + 2 * LdotH * LdotH * roughness;
	float Fd = lerp(1.0, Fd90, FL) * lerp(1.0, Fd90, FV);

	// Based on Hanrahan-Krueger brdf approximation of isotropic bssrdf
	// 1.25 scale is used to (roughly) preserve albedo
	// Fss90 used to "flatten" retroreflection based on roughness
	float Fss90 = LdotH * LdotH * roughness;
	float Fss = lerp(1.0, Fss90, FL) * lerp(1.0, Fss90, FV);
	float ss = 1.25 * (Fss * (1 / (NdotL + NdotV) - .5) + .5);

	// specular
	//float aspect = sqrt(1 - anisotropic * .9);
	//float ax = max(.001, pow2(roughness) / aspect);
	//float ay = max(.001, pow2(roughness) * aspect);
	//float Ds = GTR2_aniso(NdotH, dot(H, X), dot(H, Y), ax, ay);
	float Ds = GTR2(NdotH, max(.001, roughness * roughness));
	float FH = SchlickFresnel(LdotH);
	float3 Fs = lerp(Cspec0, float3(1, 1, 1), FH);
	//float Gs;
	//Gs = smithG_GGX_aniso(NdotL, dot(L, X), dot(L, Y), ax, ay);
	//Gs *= smithG_GGX_aniso(NdotV, dot(V, X), dot(V, Y), ax, ay);

	float Gs = G_Shlick_Smith_Hable(roughness * roughness, LdotH);

	// sheen
	float3 Fsheen = FH * sheen * Csheen;

	// clearcoat (ior = 1.5 -> F0 = 0.04)
	float Dr = GTR1(NdotH, lerp(.1, .001, clearcoatGloss));
	float Fr = lerp(.04, 1.0, FH);
	float Gr = smithG_GGX(NdotL, .25) * smithG_GGX(NdotV, .25);

	return ((1 / Disney_BRDF_PI) * lerp(Fd, ss, subsurface) * Cdlin + Fsheen)
		* (1 - metallic)
		+ Gs * Fs * Ds + .25 * clearcoat * Gr * Fr * Dr;
}
float3 brdf_s(float3 L, float3 V, float3 N, float3 X, float3 Y, float3 baseColor)
{
	//return BRDF(L, V, N, X, Y, baseColor, _Metallic, _Roughness, _Subsurface, _Specular, _SpecularTint, _Anisotropic, _Sheen, _SheenTint, _Clearcoat, _ClearcoatGloss);
	return BRDF_1(L, V, N, baseColor, _Metallic, _Roughness, _Subsurface, _Specular, _SpecularTint, _Anisotropic, _Sheen, _SheenTint, _Clearcoat, _ClearcoatGloss);
}
const static float COO_PI = 3.141592653589793238;
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

float4 main(PSSkinnedIn input) : SV_TARGET
{
	float3 strength = float3(0,0,0);
	float3 V = normalize(g_vCamPos - input.wPos);
	float3 N = normalize(input.Norm);
	float4 texColor = texture0.Sample(s1, input.TexCoord) * _DiffuseColor;
	clip(texColor.a - 0.01f);
	float3 diff = texColor.rgb;
	uint randomState = RNG::RandomSeed((uint)(input.Pos.x * input.Pos.w + (uint)(input.Pos.y * input.Pos.w) * 8192) * 8192 + g_camera_randomValue);
	float3 randomDir = normalize(float3(RNG::Random01(randomState) * 2 - 1, RNG::Random01(randomState) * 2 - 1, RNG::Random01(randomState) * 2 - 1));
	if (dot(randomDir, N) < 0)
	{
		randomDir = -randomDir;
	}
	float3 X = normalize(input.Tangent);
	float3 Y = cross(input.Norm,input.Tangent);
	//float3 inDirect = _AmbientColor;
	float3 inDirect = float3(0,0,0);
	for (int i = 0; i < 1; i++)
	{
		if (Lightings[i].LightColor.a != 0)
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			if (g_enableShadow != 0)
			{
				float4 sPos = mul(input.wPos, LightSpaceMatrices[0]);
				sPos = sPos / sPos.w;

				float2 shadowTexCoords;
				shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
				shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
				if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
					inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, float3(shadowTexCoords, 0), sPos.z).r;
				else
				{
					sPos = mul(input.wPos, LightSpaceMatrices[1]);
					sPos = sPos / sPos.w;
					float2 shadowTexCoords1;
					shadowTexCoords1.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords1.y = 0.5f - (sPos.y * 0.5f);

					if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
						inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, float3(shadowTexCoords1, 1), sPos.z).r;
				}
			}

			float3 lightDir = normalize(Lightings[i].LightDir);
			float3 lightStrength = max(Lightings[i].LightColor.rgb * Lightings[i].LightColor.a,0);

			strength += brdf_s(lightDir,V,N,X,Y,diff) * lightStrength * dot(lightDir, N) * inShadow;
		}
		else if (Lightings[i].LightType == 1)
		{
			float inShadow = 1.0f;
			float3 lightDir = normalize(Lightings[i].LightDir - input.wPos);
			float3 lightStrength = Lightings[i].LightColor.rgb * Lightings[i].LightColor.a / pow(distance(Lightings[i].LightDir, input.wPos), 2);
			strength += brdf_s(lightDir, V, N, X, Y, diff) * lightStrength * dot(lightDir, N);
		}
	}
	for (int i = 1; i < 4; i++)
	{
		if (Lightings[i].LightColor.a != 0)
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			float3 lightDir = normalize(Lightings[i].LightDir);
			float3 lightStrength = Lightings[i].LightColor.rgb * Lightings[i].LightColor.a;
			strength += brdf_s(lightDir, V, N, X, Y, diff) * lightStrength * dot(lightDir, N) * inShadow;
		}
		else if (Lightings[i].LightType == 1)
		{
			float inShadow = 1.0f;
			float3 lightDir = normalize(Lightings[i].LightDir - input.wPos);
			float3 lightStrength = Lightings[i].LightColor.rgb * Lightings[i].LightColor.a / pow(distance(Lightings[i].LightDir ,input.wPos),2);
			strength += brdf_s(lightDir, V, N, X, Y, diff) * lightStrength * dot(lightDir, N);
		}
		else
			break;
	}
	strength += _Emission * _AmbientColor;

	int sampleCount = pow(2, g_quality) * 2;
	if (dot(V, N) < 0)
	{
		N = -N;
	}
	for (int i = 0; i < sampleCount; i++)
	{
		float2 E = RNG::Hammersley(i, sampleCount, uint2(RNG::Random(randomState), RNG::Random(randomState)));
		float3 randomDir = TangentToWorld(N, RNG::HammersleySampleCos(E));

		strength += brdf_s(randomDir, V, N, X, Y, diff) * (EnvCube.Sample(s1, randomDir) * g_skyBoxMultiple + inDirect) / sampleCount * dot(randomDir, N);
	}
	//float weight = 0;
	//float3 filteredColor = float3(0, 0, 0);
	//for (int i = 0; i < sampleCount; i++)
	//{
		//float2 E = RNG::Hammersley(i, sampleCount, uint2(RNG::Random(randomState), RNG::Random(randomState)));
		//float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(_Roughness)).xyz, N);
		//float3 L = 2 * dot(V, H) * H - V;

		//float NdotL = saturate(dot(N, L));
		//if (NdotL > 0)
		//{
		//	filteredColor += brdf_s(L, V, N, X, Y, diff) * (EnvCube.Sample(s1, L) * g_skyBoxMultiple) * NdotL;
		//	weight += NdotL;
		//}
	//}
	//strength += filteredColor / max(weight, 0.001);
	return float4(strength, texColor.a);
}