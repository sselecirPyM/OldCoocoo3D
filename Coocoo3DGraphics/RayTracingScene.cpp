#include "pch.h"
#include "RayTracingScene.h"
#include "DirectXHelper.h"
#include "RayTracing/DirectXRaytracingHelper.h"
using namespace Coocoo3DGraphics;
using namespace DX;

void RayTracingScene::ReloadLibrary(IBuffer^ rtShader)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(rtShader)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* data = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&data));
	m_byteCode = CD3DX12_SHADER_BYTECODE(data, rtShader->Length);
}

inline void RayTracingScene::SubobjectHitGroup(CD3DX12_HIT_GROUP_SUBOBJECT* hitGroupSubobject, LPCWSTR hitGroupName, LPCWSTR anyHitShaderName, LPCWSTR closestHitShaderName)
{
	hitGroupSubobject->SetAnyHitShaderImport(anyHitShaderName);
	hitGroupSubobject->SetClosestHitShaderImport(closestHitShaderName);
	hitGroupSubobject->SetHitGroupExport(hitGroupName);
	hitGroupSubobject->SetHitGroupType(D3D12_HIT_GROUP_TYPE_TRIANGLES);
}

void RayTracingScene::ReloadPipelineStates(DeviceResources^ deviceResources, GraphicsSignature^ globalSignature, GraphicsSignature^ localSignature, const Platform::Array<Platform::String^>^ exportNames, const Platform::Array<HitGroupDesc^>^ hitGroups, RayTracingSceneSettings settings)
{
	m_globalSignature = globalSignature;
	m_localSignature = localSignature;
	CD3DX12_STATE_OBJECT_DESC raytracingStateObjectDesc;

	raytracingStateObjectDesc = { D3D12_STATE_OBJECT_TYPE_RAYTRACING_PIPELINE };

	auto lib = raytracingStateObjectDesc.CreateSubobject<CD3DX12_DXIL_LIBRARY_SUBOBJECT>();
	lib->SetDXILLibrary(&m_byteCode);
	for (int i = 0; i < exportNames->Length; i++)
	{
		lib->DefineExport(exportNames[i]->Begin());
	}

	m_rayTypeCount = settings.rayTypeCount;
	auto device = deviceResources->GetD3DDevice5();

	for (int i = 0; i < hitGroups->Length; i++)
	{
		SubobjectHitGroup(raytracingStateObjectDesc.CreateSubobject<CD3DX12_HIT_GROUP_SUBOBJECT>(),
			hitGroups[i]->HitGroupName->Begin(),
			hitGroups[i]->AnyHitName->Begin() != hitGroups[i]->AnyHitName->End() ? hitGroups[i]->AnyHitName->Begin() : nullptr,
			hitGroups[i]->ClosestHitName->Begin() != hitGroups[i]->ClosestHitName->End() ? hitGroups[i]->ClosestHitName->Begin() : nullptr);
	}

	auto localRootSignature = raytracingStateObjectDesc.CreateSubobject<CD3DX12_LOCAL_ROOT_SIGNATURE_SUBOBJECT>();
	localRootSignature->SetRootSignature(m_localSignature->m_rootSignature.Get());
	// Shader association
	auto rootSignatureAssociation = raytracingStateObjectDesc.CreateSubobject<CD3DX12_SUBOBJECT_TO_EXPORTS_ASSOCIATION_SUBOBJECT>();
	rootSignatureAssociation->SetSubobjectToAssociate(*localRootSignature);
	for (int i = 0; i < hitGroups->Length; i++)
	{
		rootSignatureAssociation->AddExport(hitGroups[i]->HitGroupName->Begin());
	}

	raytracingStateObjectDesc.CreateSubobject<CD3DX12_GLOBAL_ROOT_SIGNATURE_SUBOBJECT>()->SetRootSignature(m_globalSignature->m_rootSignature.Get());

	auto shaderConfig = raytracingStateObjectDesc.CreateSubobject<CD3DX12_RAYTRACING_SHADER_CONFIG_SUBOBJECT>();
	shaderConfig->Config(settings.payloadSize, settings.attributeSize);

	auto pipelineConfig = raytracingStateObjectDesc.CreateSubobject<CD3DX12_RAYTRACING_PIPELINE_CONFIG_SUBOBJECT>();
	pipelineConfig->Config(settings.maxRecursionDepth);


	DX::ThrowIfFailed(device->CreateStateObject(raytracingStateObjectDesc, IID_PPV_ARGS(&m_dxrStateObject)));
}

void RayTracingScene::ReloadAllocScratchAndInstance(DeviceResources^ deviceResources, UINT scratchSize, UINT maxInstanceCount)
{
	auto device = deviceResources->GetD3DDevice();
	m_scratchSize = scratchSize;
	m_maxInstanceCount = maxInstanceCount;
}

RayTracingScene::~RayTracingScene()
{
}
