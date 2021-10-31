#pragma once
#include "GraphicsDevice.h"
#include "RootSignature.h"
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
		void ReloadPipelineStates(GraphicsDevice^ deviceResources, RootSignature^ globalSignature, RootSignature^ localSignature, const Platform::Array<Platform::String^>^ exportNames, const Platform::Array<HitGroupDesc^>^ hitGroups, RayTracingSceneSettings settings);
		void ReloadAllocScratchAndInstance(GraphicsDevice^ deviceResources, UINT scratchSize, UINT maxIinstanceCount);
		virtual ~RayTracingScene();
	internal:
		D3D12_SHADER_BYTECODE m_byteCode;

		std::vector<CooRayTracingParamLocal1> arguments;

		//Microsoft::WRL::ComPtr<ID3D12RootSignature> m_rootSignatures[10];

		Microsoft::WRL::ComPtr<ID3D12StateObject> m_dxrStateObject;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_missShaderTable;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_hitGroupShaderTable;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_rayGenShaderTable;

		UINT m_rayGenerateShaderTableStrideInBytes;
		UINT m_hitGroupShaderTableStrideInBytes;
		UINT m_missShaderTableStrideInBytes;

		UINT m_rayTypeCount = 2;

		Microsoft::WRL::ComPtr<ID3D12Resource> m_topAS;
		std::vector<Microsoft::WRL::ComPtr<ID3D12Resource>>m_bottomLevelASs;

		Microsoft::WRL::ComPtr<ID3D12Resource> m_instanceDescs;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_scratchResource;

		std::vector <D3D12_RAYTRACING_INSTANCE_DESC> m_instanceDescDRAMs;

		UINT m_scratchSize;
		UINT m_maxInstanceCount;
		RootSignature^ m_globalSignature;
		RootSignature^ m_localSignature;
	private:
		void SubobjectHitGroup(CD3DX12_HIT_GROUP_SUBOBJECT* hitGroupSubobject, LPCWSTR hitGroupName, LPCWSTR anyHitShaderName, LPCWSTR closestHitShaderName);
	};
}
