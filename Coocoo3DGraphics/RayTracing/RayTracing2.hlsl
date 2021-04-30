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

RaytracingAccelerationStructure Scene : register(t0);
TextureCube EnvCube : register (t1);
TextureCube IrradianceCube : register (t2);
Texture2D BRDFLut : register(t3);
Texture2D ShadowMap0 : register(t4);
RWTexture2D<float4> g_renderTarget : register(u0);
cbuffer cb0 : register(b0)
{
	float4x4 g_mWorldToProj;
	float4x4 g_mProjToWorld;
	float3   g_vCamPos;
	float g_skyBoxMultiple;
};
//local
StructuredBuffer<VertexSkinned> Vertices : register(t0);
StructuredBuffer<uint> MeshIndexs : register(t1);
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
	//g_renderTarget[DispatchRaysIndex().xy] = float4(1, 0, 1, 1);
	//return;
	Ray ray = GenerateCameraRay(DispatchRaysIndex().xy, g_vCamPos.xyz, g_mProjToWorld);

	uint currentRecursionDepth = 0;

	RayDesc ray2;
	ray2.Origin = ray.origin;
	ray2.Direction = ray.direction;
	ray2.TMin = 0.001;
	ray2.TMax = 10000.0;
	RayPayload payload = { float4(0, 0, 1, 1),ray.direction,0 };
	TraceRay(Scene, RAY_FLAG_NONE, ~0, 0, 2, 0, ray2, payload);
	g_renderTarget[DispatchRaysIndex().xy] = payload.color;
}

[shader("anyhit")]
void MyAnyHitShader(inout RayPayload payload, in TriAttributes attr)
{

}

[shader("closesthit")]
void MyClosestHitShader(inout RayPayload payload, in TriAttributes attr)
{

}

[shader("miss")]
void MyMissShader(inout RayPayload payload)
{
	payload.color = float4(1, 0, 1, 1);
}

#endif // RAYTRACING_HLSL