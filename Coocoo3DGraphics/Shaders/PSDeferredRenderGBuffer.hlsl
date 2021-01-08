#include "CameraDataDefine.hlsli"
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
};

SamplerState s0 : register(s0);
SamplerState s1 : register(s1);

Texture2D texture0 :register(t0);
Texture2D texture1 :register(t1);
float2 NormalEncode(float3 n)
{
	float2 enc = normalize(n.xy) * (sqrt(-n.z * 0.5 + 0.5));
	enc = enc * 0.5 + 0.5;
	return enc;
}

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Normal : NORMAL;			//Normal
	float2 uv	: TEXCOORD;		//Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
};

struct MRTOutput
{
	float4 color0 : COLOR0;
	float4 color1 : COLOR1;
	float4 color2 : COLOR2;
};

MRTOutput main(PSSkinnedIn input) : SV_TARGET
{
	float3 N = normalize(input.Normal);
	float2 encodedNormal = NormalEncode(N);
	MRTOutput output;
	float4 color = texture0.Sample(s1, input.uv) * _DiffuseColor;
	clip(color.a - 0.98f);
	output.color0 = float4(color.rgb, _Metallic);
	output.color1 = float4(encodedNormal, _Roughness, 1);
	output.color2 = float4(1, 0, 0, 1);
	return output;
}