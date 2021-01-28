#pragma once
#include "DeviceResources.h"
#include "Interoperation/InteroperationTypes.h"
#include "GraphicsSignature.h"
#include "ShaderMacro.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class ComputePO sealed
	{
	public:
		property GraphicsObjectStatus Status;
		bool CompileInitialize1(IBuffer^ file1, Platform::String^ entryPoint, const Platform::Array<MacroEntry^>^ macros);
		void Initialize(DeviceResources^ deviceResources,GraphicsSignature^ rootSignature, IBuffer^ data);
		bool Verify(DeviceResources^ deviceResources, GraphicsSignature^ rootSignature);
		void Initialize(IBuffer^ data);
	internal:
		Microsoft::WRL::ComPtr<ID3D12PipelineState> m_pipelineState;
		Microsoft::WRL::ComPtr<ID3DBlob> m_byteCode;
	};
}
