#ifndef RAYTRACING_HLSL
#define RAYTRACING_HLSL
#include "../Shaders/BRDF/PBR.hlsli"
#include "../Shaders/CameraDataDefine.hlsli"
#include "../Shaders/RandomNumberGenerator.hlsli"

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

float3x3 GetTangentBasis(float3 TangentZ)
{
	const float Sign = TangentZ.z >= 0 ? 1 : -1;
	const float a = -rcp(Sign + TangentZ.z);
	const float b = TangentZ.x * TangentZ.y * a;

	float3 TangentX = { 1 + Sign * a * pow2(TangentZ.x), Sign * b, -Sign * TangentZ.x };
	float3 TangentY = { b,  Sign + a * pow2(TangentZ.y), -TangentZ.y };

	return float3x3(TangentX, TangentY, TangentZ);
}

float3 TangentToWorld(float3 Vec, float3 TangentZ)
{
	return mul(Vec, GetTangentBasis(TangentZ));
}

typedef BuiltInTriangleIntersectionAttributes TriAttributes;
struct RayPayload
{
	float4 color;
	float3 direction;
	uint depth;
};
static const int c_testRayIndex = 1;
struct TestRayPayload
{
	bool miss;
	float3 hitPos;
	float4 color;
};

struct LightInfo
{
	float3 LightDir;
	uint LightType;
	float4 LightColor;
};

struct VertexSkinned
{
	float3 Pos;
	float3 Norm;
	float2 Tex;
	float3 Tangent;
	float EdgeScale;
	float4 preserved1;
};

struct Ray
{
	float3 origin;
	float3 direction;
};

StructuredBuffer<VertexSkinned> VerticesX : register(t1, space2);
RWStructuredBuffer<float4>SceneLightWrite : register(u0, space2);
StructuredBuffer<float4>SceneLightRead : register(t0, space2);

RaytracingAccelerationStructure Scene : register(t0);
TextureCube EnvCube : register (t1);
TextureCube IrradianceCube : register (t2);
Texture2D BRDFLut : register(t3);
Texture2D ShadowMap0 : register(t4);
RWTexture2D<float4> g_renderTarget : register(u0);
cbuffer cb0 : register(b0)
{
	CAMERA_DATA_DEFINE;//is a macro
	float4x4 LightSpaceMatrices[1];
};
//local
StructuredBuffer<VertexSkinned> Vertices : register(t0, space1);
StructuredBuffer<uint> MeshIndexs : register(t1, space1);
Texture2D diffuseMap :register(t2, space1);
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerComparisonState sampleShadowMap0 : register(s2);
//
cbuffer cb3 : register(b3)
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
	float3 preserved0;
	float4 preserved1[4];
	uint _VertexBegin;
	uint3 preserved2;
	LightInfo Lightings[8];
};
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

float3 FilterEnvMap(uint2 Random, float Roughness, float3 N, float3 V)
{
	float3 FilteredColor = 0;
	float Weight = 0;

	const uint NumSamples = 64;
	for (uint i = 0; i < NumSamples; i++)
	{
		float2 E = RNG::Hammersley(i, NumSamples, Random);
		float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(Roughness)).xyz, N);
		float3 L = 2 * dot(V, H) * H - V;

		float NdotL = saturate(dot(N, L));
		if (NdotL > 0)
		{
			FilteredColor += EnvCube.SampleLevel(s0, L, 0).rgb * NdotL;
			Weight += NdotL;
		}
	}

	return FilteredColor / max(Weight, 0.001);
}

inline Ray GenerateCameraRay(uint2 index, in float3 cameraPosition, in float4x4 projectionToWorld)
{
	float2 xy = index + 0.5f; // center in the middle of the pixel.
	float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0 - 1.0;

	screenPos.y = -screenPos.y;

	float4 world = mul(float4(screenPos, 0, 1), projectionToWorld);
	world.xyz /= world.w;

	Ray ray;
	ray.origin = cameraPosition;
	ray.direction = normalize(world.xyz - ray.origin);

	return ray;
}

[shader("raygeneration")]
void MyRaygenShader()
{
	Ray ray = GenerateCameraRay(DispatchRaysIndex().xy, g_vCamPos.xyz, g_mProjToWorld);

	uint currentRecursionDepth = 0;


	RayDesc ray2;
	ray2.Origin = ray.origin;
	ray2.Direction = ray.direction;
	ray2.TMin = 0.001;
	ray2.TMax = 10000.0;
	RayPayload payload = { float4(0, 0, 0, 0),ray.direction,0 };
	TraceRay(Scene, RAY_FLAG_NONE, ~0, 0, 2, 0, ray2, payload);
	g_renderTarget[DispatchRaysIndex().xy] = payload.color;
}

[shader("raygeneration")]
void MyRaygenShader1()
{
	uint3 dtid = DispatchRaysIndex();
	uint randomState = RNG::RandomSeed(dtid.x + dtid.y * 8192 + g_camera_randomValue);
	int giSampleCount = g_quality * 16 + 64;
	float3 N = VerticesX[dtid.x].Norm;
	float3 V = N;
	float3 pos = VerticesX[dtid.x].Pos;
	float3 gi = float3(0, 0, 0);

	float4 cPos = mul(float4(pos, 1), g_mWorldToProj);
	float3 cPos2 = cPos.xyz / cPos.w;
	bool InCamera = (cPos2.x > -1 && cPos2.x<1 && cPos2.y>-1 && cPos2.y < 1 && cPos2.z>0 && cPos2.z < 0.5);
	bool isFront = dot(g_vCamPos - pos, N) > 0;
	if (!InCamera || !isFront)
		giSampleCount = (giSampleCount + 3) / 4;
	for (int i = 0; i < giSampleCount; i++)
	{
		RayPayload payloadGI;

		float2 E = RNG::Hammersley(i, giSampleCount, uint2(RNG::Random(randomState), RNG::Random(randomState)));
		float3 vec1 = normalize(TangentToWorld(N, RNG::HammersleySampleCos(E)));

		payloadGI.direction = vec1;

		payloadGI.color = float4(0, 0, 0, 0);
		payloadGI.depth = 2;
		RayDesc rayX;
		rayX.Origin = pos;
		rayX.Direction = payloadGI.direction;
		rayX.TMin = 1e-3f;
		rayX.TMax = 10000.0;
		TraceRay(Scene, RAY_FLAG_NONE, ~0, 0, 2, 0, rayX, payloadGI);

		float3 L = vec1;
		float3 H = normalize(L + V);
		float3 NdotL = saturate(dot(N, L));
		float3 LdotH = saturate(dot(L, H));
		float3 NdotH = saturate(dot(N, H));

		float diffuse_factor = 1;
		gi += NdotL * (payloadGI.color.rgb) * (diffuse_factor / COO_PI);
	}
	gi /= giSampleCount;
	//if(InCamera)
	//SceneLightWrite[dtid.x] = float4(1,1,1, 1);
	//else
	//for denoise
	SceneLightWrite[dtid.x] = float4(SceneLightRead[dtid.x].rgb * 0.5 + gi * 0.5, 1);
}

[shader("anyhit")]
void AnyHitShaderSurface(inout RayPayload payload, in TriAttributes attr)
{
	uint3 meshIndexs;
	meshIndexs[0] = MeshIndexs[PrimitiveIndex() * 3];
	meshIndexs[1] = MeshIndexs[PrimitiveIndex() * 3 + 1];
	meshIndexs[2] = MeshIndexs[PrimitiveIndex() * 3 + 2];

	VertexSkinned triVert[3];
	triVert[0] = Vertices[meshIndexs[0]];
	triVert[1] = Vertices[meshIndexs[1]];
	triVert[2] = Vertices[meshIndexs[2]];

	float2 uv = triVert[0].Tex * (1 - attr.barycentrics.x - attr.barycentrics.y) +
		triVert[1].Tex * (attr.barycentrics.x) +
		triVert[2].Tex * (attr.barycentrics.y);
	float4 diffuseColor = diffuseMap.SampleLevel(s0, uv, 0) * _DiffuseColor;
	if (diffuseColor.a < 0.01)
	{
		IgnoreHit();
	}
}

[shader("closesthit")]
void ClosestHitShaderSurface(inout RayPayload payload, in TriAttributes attr)
{
	payload.depth++;
	uint3 meshIndexs;
	meshIndexs[0] = MeshIndexs[PrimitiveIndex() * 3];
	meshIndexs[1] = MeshIndexs[PrimitiveIndex() * 3 + 1];
	meshIndexs[2] = MeshIndexs[PrimitiveIndex() * 3 + 2];

	float3 vertexWeight;
	vertexWeight[0] = 1 - attr.barycentrics.x - attr.barycentrics.y;
	vertexWeight[1] = attr.barycentrics.x;
	vertexWeight[2] = attr.barycentrics.y;

	VertexSkinned triVert[3];
	triVert[0] = Vertices[meshIndexs[0]];
	triVert[1] = Vertices[meshIndexs[1]];
	triVert[2] = Vertices[meshIndexs[2]];
	float3 triNorm = normalize(cross(triVert[0].Pos - triVert[1].Pos, triVert[1].Pos - triVert[2].Pos));
	float2 uv = triVert[0].Tex * vertexWeight[0] +
		triVert[1].Tex * vertexWeight[1] +
		triVert[2].Tex * vertexWeight[2];
	float3 pos = (triVert[0].Pos * vertexWeight[0] +
		triVert[1].Pos * vertexWeight[1] +
		triVert[2].Pos * vertexWeight[2]).xyz;
	float3 normal = triVert[0].Norm * vertexWeight[0] +
		triVert[1].Norm * vertexWeight[1] +
		triVert[2].Norm * vertexWeight[2];

	float3 GIVertex = SceneLightRead[meshIndexs[0] + _VertexBegin] * vertexWeight[0] +
		SceneLightRead[meshIndexs[1] + _VertexBegin] * vertexWeight[1] +
		SceneLightRead[meshIndexs[2] + _VertexBegin] * vertexWeight[2];

	float4 diffuseColor = diffuseMap.SampleLevel(s0, uv, 0) * _DiffuseColor;


	normal = normalize(normal);


	float3 V = -payload.direction;
	float3 N = normalize(normal);
	if (dot(normal, V) < 0)
	{
		N = -N;
	}
	float NdotV = saturate(dot(N, V));
	float roughness = max(_Roughness, 0.002);
	float alpha = roughness * roughness;

	float3 albedo = diffuseColor.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic);

	float3 outputColor = float3(0, 0, 0);

	uint randomState = RNG::RandomSeed(DispatchRaysIndex().x + DispatchRaysIndex().y * 8192 + g_camera_randomValue);
	if (g_enableShadow != 0)
	{
		[loop]
		for (int i = 0; i < 8; i++)
		{
			if (Lightings[i].LightColor.r > 0 || Lightings[i].LightColor.g > 0 || Lightings[i].LightColor.b > 0)
			{
				float inShadow = 1.0f;
				float3 lightStrength;
				float3 L;
				if (Lightings[i].LightType == 0)
				{
					lightStrength = max(Lightings[i].LightColor.rgb * Lightings[i].LightColor.a, 0);
					if (payload.depth > 1 && i == 0)
					{
						float4 sPos = mul(float4(pos, 1), LightSpaceMatrices[0]);
						float2 shadowTexCoords;
						shadowTexCoords.x = 0.5f + (sPos.x / sPos.w * 0.5f);
						shadowTexCoords.y = 0.5f - (sPos.y / sPos.w * 0.5f);
						if (saturate(shadowTexCoords.x) - shadowTexCoords.x == 0 && saturate(shadowTexCoords.y) - shadowTexCoords.y == 0 && g_enableShadow != 0)
							inShadow = ShadowMap0.SampleCmpLevelZero(sampleShadowMap0, shadowTexCoords, sPos.z / sPos.w).r;
						else
						{
							RayDesc ray2;
							ray2.Origin = pos;
							ray2.Direction = Lightings[i].LightDir;
							ray2.TMin = 0.001;
							ray2.TMax = 10000.0;
							TestRayPayload payload2 = { false, float3(0,0,0), float4(0,0,0,0) };
							if (payload.depth < 4 && dot(lightStrength, lightStrength)>1e-3)
								TraceRay(Scene, RAY_FLAG_NONE, ~0, c_testRayIndex, 2, c_testRayIndex, ray2, payload2);
							else
								continue;
							if (!payload2.miss)
								inShadow = 0;
						}
					}
					else
					{
						RayDesc ray2;
						ray2.Origin = pos;
						ray2.Direction = Lightings[i].LightDir;
						ray2.TMin = 0.001;
						ray2.TMax = 10000.0;
						TestRayPayload payload2 = { false, float3(0,0,0), float4(0,0,0,0) };
						if (payload.depth < 4 && dot(lightStrength, lightStrength)>1e-3)
							TraceRay(Scene, RAY_FLAG_NONE, ~0, c_testRayIndex, 2, c_testRayIndex, ray2, payload2);
						else
							continue;
						if (!payload2.miss)
							inShadow = 0;
					}

					L = normalize(Lightings[i].LightDir);
				}
				else if (Lightings[i].LightType == 1)
				{
					lightStrength = Lightings[i].LightColor.rgb * Lightings[i].LightColor.a / pow(distance(Lightings[i].LightDir, pos), 2);
					RayDesc ray2;
					ray2.Origin = pos;
					ray2.Direction = normalize(Lightings[i].LightDir - pos);
					ray2.TMin = 0.001;
					ray2.TMax = distance(Lightings[i].LightDir, pos);
					TestRayPayload payload2 = { false,float3(0,0,0),float4(0,0,0,0) };
					if (payload.depth < 4 && dot(lightStrength, lightStrength)>1e-3)
						TraceRay(Scene, RAY_FLAG_NONE, ~0, c_testRayIndex, 2, c_testRayIndex, ray2, payload2);
					else
						continue;

					if (!payload2.miss)
						inShadow = 0.0f;

					L = normalize(Lightings[i].LightDir - pos);

				}
				float3 H = normalize(L + V);
				float3 NdotL = saturate(dot(N, L));
				float3 LdotH = saturate(dot(L, H));
				float3 NdotH = saturate(dot(N, H));

				float diffuse_factor = Diffuse_Lambert(NdotL, NdotV, LdotH, roughness);
				float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

				outputColor += NdotL * lightStrength * (((c_diffuse * diffuse_factor / COO_PI) + specular_factor)) * inShadow;
			}
		}
	}
	else
	{
		[loop]
		for (int i = 0; i < 8; i++)
		{
			if (Lightings[i].LightColor.r > 0 || Lightings[i].LightColor.g > 0 || Lightings[i].LightColor.b > 0)
			{
				float inShadow = 1.0f;
				float3 lightStrength;
				float3 L;
				if (Lightings[i].LightType == 0)
				{
					lightStrength = max(Lightings[i].LightColor.rgb * Lightings[i].LightColor.a, 0);

					L = normalize(Lightings[i].LightDir);
				}
				else if (Lightings[i].LightType == 1)
				{
					lightStrength = Lightings[i].LightColor.rgb * Lightings[i].LightColor.a / pow(distance(Lightings[i].LightDir, pos), 2);

					L = normalize(Lightings[i].LightDir - pos);
				}
				float3 H = normalize(L + V);
				float3 NdotL = saturate(dot(N, L));
				float3 LdotH = saturate(dot(L, H));
				float3 NdotH = saturate(dot(N, H));

				float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);
				float3 specular_factor = Specular_BRDF(alpha, c_specular, NdotV, NdotL, LdotH, NdotH);

				outputColor += NdotL * lightStrength * (((c_diffuse * diffuse_factor / COO_PI) + specular_factor)) * inShadow;
			}
		}
	}


	float2 AB = BRDFLut.SampleLevel(s0, float2(NdotV, 1 - roughness), 0).rg;
	float3 GF = c_specular * AB.x + AB.y;

	uint budget = pow(2, g_quality) * 8;
	uint diffuseBudget = clamp(roughness * budget * (1 - _Metallic * 0.8) * 0.8, 0, budget);
	uint specularBudget = budget - diffuseBudget;
	if (payload.depth < 2 && g_enableAO != 0 && diffuseBudget>0)
	{
		float3 gi = 0;
		for (int i = 0; i < diffuseBudget; i++)
		{
			RayPayload payloadGI;

			float2 E = RNG::Hammersley(i, diffuseBudget, uint2(RNG::Random(randomState), RNG::Random(randomState)));
			float3 vec1 = normalize(TangentToWorld(N, RNG::HammersleySampleCos(E)));

			payloadGI.direction = vec1;

			payloadGI.color = float4(0, 0, 0, 0);
			payloadGI.depth = 2;
			RayDesc rayX;
			rayX.Origin = pos;
			rayX.Direction = payloadGI.direction;
			rayX.TMin = 1e-3f;
			rayX.TMax = 10000.0;
			TraceRay(Scene, RAY_FLAG_NONE, ~0, 0, 2, 0, rayX, payloadGI);

			float3 L = vec1;
			float3 H = normalize(L + V);
			float3 NdotL = saturate(dot(N, L));
			float3 LdotH = saturate(dot(L, H));
			float3 NdotH = saturate(dot(N, H));

			float diffuse_factor = Diffuse_Burley(NdotL, NdotV, LdotH, roughness);

			gi += NdotL * payloadGI.color.rgb * (c_diffuse * diffuse_factor / COO_PI);
		}
		if (diffuseBudget > 4)
			outputColor += gi / diffuseBudget;
		else
			outputColor += gi / diffuseBudget * 0.75 + GIVertex * albedo.rgb * c_diffuse * 0.25;
	}
	else
	{
		outputColor += GIVertex * albedo.rgb * c_diffuse;
	}

	if (payload.depth < 3 && specularBudget > 0)
	{
		float3 filteredColor = 0;
		float weight = 0;
		if (payload.depth == 2)
			specularBudget = min(1, specularBudget);
		for (int i = 0; i < specularBudget; i++)
		{
			float2 E = RNG::Hammersley(i, specularBudget, uint2(RNG::Random(randomState), RNG::Random(randomState)));
			float3 H = TangentToWorld(ImportanceSampleGGX(E, Pow4(roughness)).xyz, N);
			float3 L = 2 * dot(V, H) * H - V;

			float NdotL = saturate(dot(N, L));
			if (NdotL > 0)
			{
				RayPayload payloadReflect;
				payloadReflect.direction = L;
				payloadReflect.color = float4(0, 0, 0, 0);
				payloadReflect.depth = payload.depth;
				RayDesc rayX;
				rayX.Origin = pos;
				rayX.Direction = payloadReflect.direction;
				rayX.TMin = 1e-3f;
				rayX.TMax = 10000.0;
				TraceRay(Scene, RAY_FLAG_NONE, ~0, 0, 2, 0, rayX, payloadReflect);

				filteredColor += payloadReflect.color.rgb * NdotL;
				weight += NdotL;
			}
		}
		outputColor += filteredColor * GF / max(weight, 0.001);
	}
	else
	{
		outputColor += EnvCube.SampleLevel(s0, reflect(-V, N), sqrt(max(roughness, 1e-5)) * 6) * g_skyBoxMultiple * GF;
	}
	outputColor += _Emission * _AmbientColor;

	payload.color = float4(payload.color.rgb + (1 - payload.color.a) * outputColor, payload.color.a + diffuseColor.a - payload.color.a * diffuseColor.a);

	RayDesc rayNext;
	rayNext.Origin = pos;
	rayNext.Direction = payload.direction;
	rayNext.TMin = 5e-4f;
	rayNext.TMax = 10000.0;
	if (payload.depth < 2 && payload.color.a < 0.99)
		TraceRay(Scene, RAY_FLAG_NONE, ~0, 0, 2, 0, rayNext, payload);
}

[shader("miss")]
void MissShaderSurface(inout RayPayload payload)
{
	payload.color += EnvCube.SampleLevel(s0, payload.direction, payload.depth) * g_skyBoxMultiple;
}


[shader("anyhit")]
void AnyHitShaderTest(inout TestRayPayload payload, in TriAttributes attr)
{
	uint3 meshIndexs;
	meshIndexs[0] = MeshIndexs[PrimitiveIndex() * 3];
	meshIndexs[1] = MeshIndexs[PrimitiveIndex() * 3 + 1];
	meshIndexs[2] = MeshIndexs[PrimitiveIndex() * 3 + 2];

	VertexSkinned triVert[3];
	triVert[0] = Vertices[meshIndexs[0]];
	triVert[1] = Vertices[meshIndexs[1]];
	triVert[2] = Vertices[meshIndexs[2]];

	float2 uv = triVert[0].Tex * (1 - attr.barycentrics.x - attr.barycentrics.y) +
		triVert[1].Tex * (attr.barycentrics.x) +
		triVert[2].Tex * (attr.barycentrics.y);
	float4 diffuseColor = diffuseMap.SampleLevel(s0, uv, 0) * _DiffuseColor;
	if (diffuseColor.a < 0.5)
	{
		IgnoreHit();
	}
}

[shader("closesthit")]
void ClosestHitShaderTest(inout TestRayPayload payload, in TriAttributes attr)
{
	uint3 meshIndexs;
	meshIndexs[0] = MeshIndexs[PrimitiveIndex() * 3];
	meshIndexs[1] = MeshIndexs[PrimitiveIndex() * 3 + 1];
	meshIndexs[2] = MeshIndexs[PrimitiveIndex() * 3 + 2];

	VertexSkinned triVert[3];
	triVert[0] = Vertices[meshIndexs[0]];
	triVert[1] = Vertices[meshIndexs[1]];
	triVert[2] = Vertices[meshIndexs[2]];

	payload.miss = false;
	float3 pos = (triVert[0].Pos * (1 - attr.barycentrics.x - attr.barycentrics.y) +
		triVert[1].Pos * (attr.barycentrics.x) +
		triVert[2].Pos * (attr.barycentrics.y)).xyz;
	float2 uv = (triVert[0].Tex * (1 - attr.barycentrics.x - attr.barycentrics.y) +
		triVert[1].Tex * (attr.barycentrics.x) +
		triVert[2].Tex * (attr.barycentrics.y));

	payload.hitPos = pos;
	payload.color = diffuseMap.SampleLevel(s0, uv, 0) * _DiffuseColor;
}

[shader("miss")]
void MissShaderTest(inout TestRayPayload payload)
{
	payload.miss = true;
}
#endif // RAYTRACING_HLSL