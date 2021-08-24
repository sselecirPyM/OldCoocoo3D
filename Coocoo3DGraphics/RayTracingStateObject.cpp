#include "pch.h"
#include "RayTracingStateObject.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;

//void RayTracingStateObject::LoadShaderLib(IBuffer^ rtShader)
//{
//	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
//	reinterpret_cast<IInspectable*>(rtShader)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
//	byte* data = nullptr;
//	DX::ThrowIfFailed(bufferByteAccess->Buffer(&data));
//	m_byteCode = CD3DX12_SHADER_BYTECODE(data, rtShader->Length);
//}
//
//void RayTracingStateObject::ExportLib(const Platform::Array<Platform::String^>^ exportNames)
//{
//	auto lib = raytracingStateObjectDesc.CreateSubobject<CD3DX12_DXIL_LIBRARY_SUBOBJECT>();
//	lib->SetDXILLibrary(&m_byteCode);
//	for (int i = 0; i < exportNames->Length; i++)
//	{
//		lib->DefineExport(exportNames[i]->Begin());
//	}
//}
//
//inline void SubobjectHitGroup(CD3DX12_HIT_GROUP_SUBOBJECT* hitGroupSubobject, LPCWSTR hitGroupName, LPCWSTR anyHitShaderName, LPCWSTR closestHitShaderName)
//{
//	hitGroupSubobject->SetAnyHitShaderImport(anyHitShaderName);
//	hitGroupSubobject->SetClosestHitShaderImport(closestHitShaderName);
//	hitGroupSubobject->SetHitGroupExport(hitGroupName);
//	hitGroupSubobject->SetHitGroupType(D3D12_HIT_GROUP_TYPE_TRIANGLES);
//}
//
//void RayTracingStateObject::HitGroupSubobject(Platform::String^ HitGroupName, Platform::String^ AnyHitName, Platform::String^ ClosestHitName)
//{
//	SubobjectHitGroup(raytracingStateObjectDesc.CreateSubobject<CD3DX12_HIT_GROUP_SUBOBJECT>(),
//		HitGroupName->Begin(),
//		AnyHitName->Begin() != AnyHitName->End() ? AnyHitName->Begin() : nullptr,
//		ClosestHitName->Begin() != ClosestHitName->End() ? ClosestHitName->Begin() : nullptr);
//	HitGroupDesc2^ desc = ref new HitGroupDesc2();
//	desc->HitGroupName = HitGroupName;
//	desc->AnyHitName = AnyHitName;
//	desc->ClosestHitName = ClosestHitName;
//	hitGroupDescs.push_back(desc);
//}
//
//void RayTracingStateObject::LocalRootSignature(GraphicsSignature^ signature)
//{
//	auto rootSignatureAssociation = raytracingStateObjectDesc.CreateSubobject<CD3DX12_SUBOBJECT_TO_EXPORTS_ASSOCIATION_SUBOBJECT>();
//	auto localRootSignature = raytracingStateObjectDesc.CreateSubobject<CD3DX12_LOCAL_ROOT_SIGNATURE_SUBOBJECT>();
//	localRootSignature->SetRootSignature(signature->m_rootSignature.Get());
//	rootSignatureAssociation->SetSubobjectToAssociate(*localRootSignature);
//
//	for (int i = 0; i < hitGroupDescs.size(); i++)
//	{
//		rootSignatureAssociation->AddExport(hitGroupDescs[i]->HitGroupName->Begin());
//	}
//}
//
//void RayTracingStateObject::GlobalRootSignature(GraphicsSignature^ signature)
//{
//	m_globalRootSignature = signature;
//	raytracingStateObjectDesc.CreateSubobject<CD3DX12_GLOBAL_ROOT_SIGNATURE_SUBOBJECT>()->SetRootSignature(signature->m_rootSignature.Get());
//}
//
//void RayTracingStateObject::Config(int payloadSize, int attributeSize, int maxRecursionDepth)
//{
//	auto shaderConfig = raytracingStateObjectDesc.CreateSubobject<CD3DX12_RAYTRACING_SHADER_CONFIG_SUBOBJECT>();
//	shaderConfig->Config(payloadSize, attributeSize);
//
//	auto pipelineConfig = raytracingStateObjectDesc.CreateSubobject<CD3DX12_RAYTRACING_PIPELINE_CONFIG_SUBOBJECT>();
//	pipelineConfig->Config(maxRecursionDepth);
//}
//
//void RayTracingStateObject::Create(DeviceResources^ deviceResource)
//{
//	auto device = deviceResource->GetD3DDevice5();
//	DX::ThrowIfFailed(device->CreateStateObject(raytracingStateObjectDesc, IID_PPV_ARGS(&m_dxrStateObject)));
//}
