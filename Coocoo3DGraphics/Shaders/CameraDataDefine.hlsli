//这个文件仅定义Camera Data的结构
//开辟新渲染管线时需要使用它
//It just define Camera Data Struct.
//Please use it in new render pipeline.
#define CAMERA_DATA_DEFINE \
	float4x4 g_mWorldToProj;\
	float4x4 g_mProjToWorld;\
	float3   g_vCamPos;\
	float g_aspectRatio;\
	float g_time;\
	float g_deltaTime;\
	uint2 g_camera_randomValue;\
	float g_skyBoxMultiple;\
	uint g_enableAO;\
	uint g_enableShadow;\
	uint g_quality;\
	float4 g_camera_preserved2[5]