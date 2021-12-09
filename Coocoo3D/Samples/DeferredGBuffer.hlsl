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

cbuffer cb1 : register(b1)
{
	float4x4 g_mWorld;
	float4x4 g_mWorldToProj;
	float _Metallic;
	float _Roughness;
	float _Emission;
	float _Specular;
	float4 _DiffuseColor;
	float3 g_vCamPos;
	float g_skyBoxMultiple;
}

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
};

PSSkinnedIn vsmain(VSSkinnedIn input)
{
	PSSkinnedIn output;

	SkinnedInfo vSkinned = SkinVert(input);
	float3 pos = mul(vSkinned.Pos, g_mWorld);
	output.Norm = normalize(mul(vSkinned.Norm, (float3x3)g_mWorld));
	output.Tangent = normalize(mul(vSkinned.Tan, (float3x3)g_mWorld));
	output.Tex = input.Tex;
	//float3 pos = input.Pos;
	//output.Norm = input.Norm;
	//output.Tangent = input.Tan;
	//output.Tex = input.Tex;

	output.Pos = mul(float4(pos, 1), g_mWorldToProj);
	output.wPos = float4(pos, 1);

	return output;
}

SamplerState s0 : register(s0);
SamplerState s1 : register(s1);

Texture2D texture0 :register(t0);
Texture2D Emission :register(t1);
float2 NormalEncode(float3 n)
{
	float2 enc = normalize(n.xy) * (sqrt(-n.z * 0.5 + 0.5));
	enc = enc * 0.5 + 0.5;
	return enc;
}

struct MRTOutput
{
	float4 color0 : COLOR0;
	float4 color1 : COLOR1;
	float4 color2 : COLOR2;
	float4 color3 : COLOR3;
};

MRTOutput psmain(PSSkinnedIn input) : SV_TARGET
{
	float3 N = normalize(input.Norm);
	float2 encodedNormal = NormalEncode(N);
	MRTOutput output;
	float4 color = texture0.Sample(s1, input.Tex) * _DiffuseColor;
	//clip(color.a - 0.98f);
	float roughness = max(_Roughness, 0.002);
	float alpha = roughness * roughness;

	float3 albedo = color.rgb;

	float3 c_diffuse = lerp(albedo * (1 - _Specular * 0.08f), 0, _Metallic);
	float3 c_specular = lerp(_Specular * 0.08f, albedo, _Metallic);
	float3 emission = Emission.Sample(s1, input.Tex) * _Emission;

	output.color0 = float4(c_diffuse, 1);
	output.color1 = float4(encodedNormal, _Roughness, 1);
	output.color2 = float4(c_specular, 1);
	output.color3 = float4(emission, 1);
	return output;
}