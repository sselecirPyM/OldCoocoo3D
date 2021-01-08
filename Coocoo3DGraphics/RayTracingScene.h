#pragma once
#include "DeviceResources.h"
#include "GraphicsSignature.h"
#include "MMDMesh.h"
namespace Coocoo3DGraphics
{
	//params equal as local root signature
	struct CooRayTracingParamLocal1
	{
		D3D12_GPU_VIRTUAL_ADDRESS cbv3;
		D3D12_GPU_VIRTUAL_ADDRESS srv0_1;
		D3D12_GPU_VIRTUAL_ADDRESS srv1_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv2_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv3_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv4_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv5_1;
		D3D12_GPU_DESCRIPTOR_HANDLE srv6_1;
	};
	using namespace Windows::Storage::Streams;
	public ref class HitGroupDesc sealed
	{
	public:
		property Platform::String^ HitGroupName;
		property Platform::String^ AnyHitName;
		property Platform::String^ ClosestHitName;
	};
	public value struct RayTracingSceneSettings
	{
		UINT payloadSize;
		UINT attributeSize;
		UINT maxRecursionDepth;
		UINT rayTypeCount;
	};
	public ref class RayTracingScene sealed
	{
	public:
		void ReloadLibrary(IBuffer^ rtShader);
		void ReloadPipelineStates(DeviceResources^ deviceResources,const Platform::Array<Platform::String^>^ exportNames, const Platform::Array<HitGroupDesc^>^ hitGroups, RayTracingSceneSettings settings);
		void ReloadAllocScratchAndInstance(DeviceResources^ deviceResources, UINT scratchSize, UINT maxIinstanceCount);
		void NextASIndex(int meshCount);
		void NextSTIndex();
		void BuildShaderTable(DeviceResources^ deviceResources, const Platform::Array<Platform::String^>^ raygenShaderNames, const Platform::Array<Platform::String^>^ missShaderNames, const Platform::Array <Platform::String^>^ hitGroupNames, int instances);
		virtual ~RayTracingScene();
	internal:
		D3D12_SHADER_BYTECODE m_byteCode;

		static const int c_argumentCacheStride = sizeof(CooRayTracingParamLocal1);

		Microsoft::WRL::ComPtr<ID3D12RootSignature> m_rootSignatures[10];

		Microsoft::WRL::ComPtr<ID3D12StateObject> m_dxrStateObject;
		int stLastUpdateIndex = 0;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_missShaderTable[c_frameCount];
		Microsoft::WRL::ComPtr<ID3D12Resource> m_hitGroupShaderTable[c_frameCount];
		Microsoft::WRL::ComPtr<ID3D12Resource> m_rayGenShaderTable[c_frameCount];

		UINT m_rayGenerateShaderTableStrideInBytes;
		UINT m_hitGroupShaderTableStrideInBytes;
		UINT m_missShaderTableStrideInBytes;

		UINT m_rayTypeCount = 2;

		Microsoft::WRL::ComPtr<ID3D12Resource> m_topLevelAccelerationStructure[c_frameCount];
		UINT m_topLevelAccelerationStructureSize[c_frameCount] = {};
		std::vector<Microsoft::WRL::ComPtr<ID3D12Resource>>m_bottomLevelASs[c_frameCount];

		Microsoft::WRL::ComPtr<ID3D12Resource> instanceDescs[c_frameCount];
		Microsoft::WRL::ComPtr<ID3D12Resource> m_scratchResource[c_frameCount];
		int asLastUpdateIndex = 0;

		std::vector <D3D12_RAYTRACING_INSTANCE_DESC> m_instanceDescs;

		void* pArgumentCache = nullptr;
	private:
		void SubobjectHitGroup(CD3DX12_HIT_GROUP_SUBOBJECT* hitGroupSubobject, LPCWSTR hitGroupName, LPCWSTR anyHitShaderName, LPCWSTR closestHitShaderName);
	};
}
