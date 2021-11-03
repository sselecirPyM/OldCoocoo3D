#define MAX_BONE_MATRICES 1020
cbuffer cbAnimMatrices : register(b0)
{
	float4x4 g_mWorld;
	float g_posAmount1;
	float3 g_bonePreserved1;
	float4 g_bonePreserved3[3];
	float4x4 g_bonePreserved2[2];
	float4x4 g_mConstBoneWorld[MAX_BONE_MATRICES];
};

struct VSSkinnedIn
{
	float3 Pos	: POSITION0;			//Position
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

#define CAMERA_DATA_DEFINE \
	float4x4 g_mWorldToProj;\
	float4x4 g_mProjToWorld;\
	float3   g_vCamPos;\
	float g_skyBoxMultiple;\
	uint g_enableAO;\
	uint g_enableShadow;\
	uint g_quality;\
	float g_aspectRatio;\
	uint2 g_camera_randomValue;\
	float4 g_camera_preserved2[5]
cbuffer cb2 : register(b1)
{
	CAMERA_DATA_DEFINE;//is a macro
};

struct PSSkinnedIn
{
	float4 Pos	: SV_POSITION;		//Position
	float4 wPos	: POSITION;			//world space Pos
	float3 Norm : NORMAL;			//Normal
	float2 Tex	: TEXCOORD;		    //Texture coordinate
	float3 Tangent : TANGENT;		//Normalized Tangent vector
};

PSSkinnedIn main(VSSkinnedIn input)
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