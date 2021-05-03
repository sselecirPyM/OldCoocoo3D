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