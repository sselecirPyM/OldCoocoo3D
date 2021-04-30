#pragma once
#include "GraphicsSignature.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class HitGroupDesc2 sealed
	{
	public:
		property Platform::String^ HitGroupName;
		property Platform::String^ AnyHitName;
		property Platform::String^ ClosestHitName;
	};
	public ref class RayTracingStateObject sealed
	{
	public:
		void LoadShaderLib(IBuffer^ rtShader);
		void ExportLib(const Platform::Array<Platform::String^>^ exportNames);
		void HitGroupSubobject(Platform::String^ HitGroupName, Platform::String^ AnyHitName, Platform::String^ ClosestHitName);
		void LocalRootSignature(GraphicsSignature^ signature);
		void GlobalRootSignature(GraphicsSignature^ signature);
		void Config(int payloadSize, int attributeSize, int maxRecursionDepth);
		void Create(DeviceResources^ deviceResource);
	internal:
		CD3DX12_STATE_OBJECT_DESC raytracingStateObjectDesc = { D3D12_STATE_OBJECT_TYPE_RAYTRACING_PIPELINE };
		Microsoft::WRL::ComPtr<ID3D12StateObject> m_dxrStateObject;
		UINT m_rayTypeCount = 2;
		std::vector<HitGroupDesc2^> hitGroupDescs;
		D3D12_SHADER_BYTECODE m_byteCode;
		GraphicsSignature^ m_globalRootSignature;
	};
}
