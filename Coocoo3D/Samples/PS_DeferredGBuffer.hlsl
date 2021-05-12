struct LightInfo2
{
	float4x4 LightMapVP;
	float3 LightDir;
	uint LightType;
	float4 LightColor;
};
cbuffer cb0 : register(b0)
{
	float4x4 _worldToProj;
	LightInfo2 Lightings2[1];
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
	//clip(color.a - 0.98f);
	float roughness = max(_Roughness, 0.002);
	float alpha = roughness * roughness;

	float3 albedo = color.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic);

	output.color0 = float4(c_diffuse, _Metallic);
	output.color1 = float4(encodedNormal, _Roughness, 1);
	output.color2 = float4(c_specular, 1);
	return output;
}