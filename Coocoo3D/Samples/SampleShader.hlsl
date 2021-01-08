//VS、VS1、GS1、PS1这四个函数用于渲染
//VS能在光线追踪中使用
//CSParticle用于进行粒子发射和更新。但是在这个版本不能使用，请等待以后更新。
//随着版本更新，这个文件的内容需要相应改动。register也可能会改动。不能保证兼容性。
//使用shader model 5.0
//更多信息请在Coocoo3D源码中的Shaders文件夹里找
#define MAX_BONE_MATRICES 1020

struct LightInfo
{
	float3 LightDir;
	uint LightType;
	float4 LightColor;
};

cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mWorld;
	float g_posAmount1;
	uint g_vertexCount;
	uint g_indexCount;
	float cb1_preserved1;
	float4 g_bonePreserved3[3];
	float4x4 g_bonePreserved2[2];
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
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
	LightInfo Lightings[4];
};
cbuffer cb2 : register(b2)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_vCamPos;
	float g_aspectRatio;
	float g_time;
	float g_deltaTime;
	uint2 g_camera_randomValue;
	float g_skyBoxMultiple;
	uint g_enableAO;
	uint g_enableShadow;
	uint g_quality;
	float4 g_camera_preserved2[5];
};

struct VSSkinnedIn
{
	float3 Pos	: POSITION0;		//Position
	float3 Pos1	: POSITION1;		//Position
	float4 Weights : WEIGHTS;		//Bone weights
	uint4  Bones : BONES;			//Bone indices
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tan : TANGENT;		    //Normalized Tangent vector
	float EdgeScale : EDGESCALE;
};

struct VSSkinnedOut
{
	float3 Pos	: POSITION;			//Position
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
	float EdgeScale : EDGESCALE;
};


// Create an initial random number for this thread
uint SeedThread(uint seed)
{
	// Thomas Wang hash 
	// Ref: http://www.burtleburtle.net/bob/hash/integer.html
	seed = (seed ^ 61) ^ (seed >> 16);
	seed *= 9;
	seed = seed ^ (seed >> 4);
	seed *= 0x27d4eb2d;
	seed = seed ^ (seed >> 15);
	return seed;
}
// Generate a random 32-bit integer
uint Random(inout uint state)
{
	// Xorshift algorithm from George Marsaglia's paper.
	state ^= (state << 13);
	state ^= (state >> 17);
	state ^= (state << 5);
	return state;
}
// Generate a random float in the range [0.0f, 1.0f)
float Random01(inout uint state)
{
	return asfloat(0x3f800000 | Random(state) >> 9) - 1.0;
}


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
float1 pow5(float1 x)
{
	return x * x * x * x * x;
}

// Shlick's approximation of Fresnel
// https://en.wikipedia.org/wiki/Schlick%27s_approximation
float3 Fresnel_Shlick(in float3 f0, in float3 f90, in float x)
{
	return f0 + (f90 - f0) * pow5(1.f - x);
}

float3 Fresnel_SchlickRoughness(float cosTheta, float3 F0, float roughness)
{
	return F0 + (max(1.0 - roughness, F0) - F0) * pow(1.0 - cosTheta, 5.0);
}

// Burley B. "Physically Based Shading at Disney"
// SIGGRAPH 2012 Course: Practical Physically Based Shading in Film and Game Production, 2012.
float Diffuse_Burley(in float NdotL, in float NdotV, in float LdotH, in float roughness)
{
	float fd90 = 0.5f + 2.f * roughness * LdotH * LdotH;
	return Fresnel_Shlick(1, fd90, NdotL).x * Fresnel_Shlick(1, fd90, NdotV).x;
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

#if COO_SURFACE
struct SkinnedInfo
{
	float4 Pos;
	float3 Norm;
	float3 Tan;
};

matrix FetchBoneTransform(uint iBone)
{
	return g_mConstBoneWorld[iBone];
}

SkinnedInfo SkinVert(VSSkinnedIn Input)
{
	SkinnedInfo Output = (SkinnedInfo)0;

	float4 Pos = float4(Input.Pos * (1 - g_posAmount1) + Input.Pos1 * g_posAmount1, 1);
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

VSSkinnedOut VS(VSSkinnedIn input)
{
	VSSkinnedOut output;

	SkinnedInfo vSkinned = SkinVert(input);
	output.Pos = mul(vSkinned.Pos, g_mWorld);
	output.Norm = normalize(mul(vSkinned.Norm, (float3x3)g_mWorld));
	output.Tangent = normalize(mul(vSkinned.Tan, (float3x3)g_mWorld));
	output.Tex = input.Tex;
	output.EdgeScale = input.EdgeScale;

	return output;
}

SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
Texture2D texture0 :register(t0);
Texture2D texture1 :register(t1);
Texture2D ShadowMap0:register(t2);
TextureCube EnvCube : register (t3);
TextureCube IrradianceCube : register (t4);
Texture2D BRDFLut : register(t5);
Texture2D ShadowMap1:register(t6);
struct PSIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 TexCoord	: TEXCOORD;		//Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
};
float4 PS1(PSIn input) : SV_TARGET
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
			if (g_enableShadow != 0)
			{
				float4 sPos = mul(input.wPos, LightSpaceMatrices[0]);
				sPos = sPos / sPos.w;

				float2 shadowTexCoords;
				shadowTexCoords.x = 0.5f + (sPos.x * 0.5f);
				shadowTexCoords.y = 0.5f - (sPos.y * 0.5f);
				if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
					inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords,sPos.z).r;
				else
				{
					sPos = mul(input.wPos, LightSpaceMatrices[1]);
					sPos = sPos / sPos.w;
					float2 shadowTexCoords1;
					shadowTexCoords1.x = 0.5f + (sPos.x * 0.5f);
					shadowTexCoords1.y = 0.5f - (sPos.y * 0.5f);

					if (sPos.x >= -1 && sPos.x <= 1 && sPos.y >= -1 && sPos.y <= 1)
						inShadow = ShadowMap1.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords1, sPos.z).r;
				}
			}

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
	for (int i = 1; i < 8; i++)
	{
		if (Lightings[i].LightColor.a == 0)continue;
		if (Lightings[i].LightType == 0)
		{
			float inShadow = 1.0f;
			float3 lightStrength = max(Lightings[i].LightColor.rgb * Lightings[i].LightColor.a, 0);

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
		else
			break;
	}
	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	outputColor += IrradianceCube.Sample(s0, N) * g_skyBoxMultiple * c_diffuse;
	outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 6) * g_skyBoxMultiple * GF /** Fresnel_Shlick(c_specular, grazingTerm, NdotV)*/;
	outputColor += _Emission * _AmbientColor;

	return float4(outputColor * float3(0.5, 0.5, 1), texColor.a);
	return float4(float3(0.5,1,1), 1);
}
#endif //COO_SURFACE
#ifdef COO_PARTICLE

struct Vertex1
{
	float3 Pos;
	float3 Norm;
	float2 Tex;
	float3 Tangent;
	float EdgeScale;
	float4 preserved;
};
struct Triangle1
{
	Vertex1 verts[3];
};
struct ParticleDynamicData
{
	float3 position;
	float3 verts[3];
	float3 speed;
	float life;
};

//StructuredBuffer<Triangle1> sourcePrimitives : register(t0);
RWStructuredBuffer<Triangle1> targetPrimitives : register(u0);
RWStructuredBuffer<ParticleDynamicData> particleDynamicData : register(u1);

//[numthreads(64, 1, 1)]
//void CSParticle(uint3 dtid : SV_DispatchThreadID)
//{
//	uint index = dtid.x;
//	if (index >= g_indexCount / 3)return;//防止内存访问溢出，溢出会造成严重后果。
//	Triangle1 tri1 = targetPrimitives[index];
//	if (particleDynamicData[index].life <= 0)
//	{
//		uint seed = SeedThread(index + g_camera_randomValue);
//
//		particleDynamicData[index].life = 5 * Random01(seed);
//		particleDynamicData[index].position = tri1.verts[0].Pos + tri1.verts[1].Pos + tri1.verts[2].Pos;
//		particleDynamicData[index].speed = float3(Random01(seed) - 0.5f, -3 * Random01(seed), Random01(seed) - 0.5f);
//		for (int i = 0; i < 3; i++)
//		{
//			particleDynamicData[index].verts[i] = tri1.verts[i].Pos;
//		}
//	}
//	else
//	{
//		particleDynamicData[index].life -= g_deltaTime;
//
//		for (int i = 0; i < 3; i++)
//		{
//			particleDynamicData[index].verts[i] += particleDynamicData[index].speed * g_deltaTime;
//		}
//	}
//	for (int i = 0; i < 3; i++)
//	{
//		tri1.verts[i].Pos = particleDynamicData[index].verts[i];
//	}
//	targetPrimitives[index] = tri1;
//}
#endif