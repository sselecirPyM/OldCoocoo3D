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
	LightInfo Lightings[1];
	PointLightInfo PointLights[POINT_LIGHT_COUNT];
};
Texture2D texture0 :register(t0);
Texture2D texture1 :register(t1);
Texture2D texture2 :register(t2);
Texture2D texture3 :register(t3);
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
#define ENABLE_DIFFUSE 1
#define ENABLE_SPECULR 1

#ifdef DEBUG_SPECULAR_RENDER
#undef ENABLE_DIFFUSE
#endif

#ifdef DEBUG_DIFFUSE_RENDER
#undef ENABLE_SPECULR
#endif

float4 psmain(PSIn input) : SV_TARGET
{
	float2 uv = input.uv * 0.5 + 0.5;
	uv.y = 1 - uv.y;

	float depth1 = gbufferDepth.SampleLevel(s3, uv, 0).r;
	float4 buffer0Color = texture0.Sample(s3, uv);
	float4 buffer1Color = texture1.Sample(s3, uv);
	float4 buffer2Color = texture2.Sample(s3, uv);
	float4 buffer3Color = texture3.Sample(s3, uv);


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
		float metallic = buffer0Color.a;
		float roughness = buffer1Color.b;
		float alpha = roughness * roughness;
		float3 c_diffuse = buffer0Color.rgb;
		float3 c_specular = buffer2Color.rgb;
		float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
		float3 GF = c_specular * AB.x + AB.y;
		float3 emission = buffer3Color.rgb;

#if ENABLE_DIFFUSE
		outputColor += EnvCube.SampleLevel(s0, N, 5) * g_skyBoxMultiple * c_diffuse;
#endif
#if ENABLE_SPECULR
		outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 4) * g_skyBoxMultiple * GF;
#endif
		outputColor += emission;

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
#if DEBUG_EMISSION
		return float4(emission, 1);
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
#if ENABLE_LIGHT
		for (int i = 0; i < 1; i++)
		{
			if (!any(Lightings[i].LightColor))continue;
			if (Lightings[i].LightType == 0)
			{
				float inShadow = 1.0f;
				float3 lightStrength = max(Lightings[i].LightColor.rgb, 0);
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
#endif//ENABLE_LIGHT
#if ENABLE_FOG
		outputColor = lerp(_fogColor, outputColor, 1 / exp(max(camDist - _startDistance, 0.00001) * _fogDensity));
#endif
	}
	else
	{
		outputColor = SkyBox.Sample(s0, -V).rgb * g_skyBoxMultiple;
	}
#if ENABLE_LIGHT
#if ENABLE_VOLUME_LIGHTING
		//const static int volumeLightIterCount = 128;
		//const static float volumeLightMaxDistance = 128;
		//const static float volumeLightIntensity = 0.0001f;
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
					if (j * volumeLightIterStep > camDist)
					{
						break;
					}
					float inShadow = 1.0f;
					float4 samplePos = float4(g_camPos - V * (volumeLightIterStep * j + offset), 1);
					float4 sPos = mul(samplePos, Lightings[i].LightMapVP);
					sPos = sPos / sPos.w;

					float2 shadowTexCoords;
					shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
					if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
						inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, float3(shadowTexCoords, 0), sPos.z).r;

					outputColor += inShadow * lightStrength * volumeLightIterStep * volumeLightIntensity;
				}
			}
		}
#endif
#endif
	return float4(outputColor, 1);
}