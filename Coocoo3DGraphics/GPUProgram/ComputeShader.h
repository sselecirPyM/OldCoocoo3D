#pragma once
#include "Interoperation/InteroperationTypes.h"
#include "GraphicsSignature.h"
#include "ShaderMacro.h"
namespace Coocoo3DGraphics
{
	struct _computeShaderDesc
	{
		ID3D12RootSignature* rootSignature;
		Microsoft::WRL::ComPtr<ID3D12PipelineState> pipelineState;
	};
	using namespace Windows::Storage::Streams;
	public ref class ComputeShader sealed
	{
	public:
		property GraphicsObjectStatus Status;
		bool CompileInitialize1(IBuffer^ file1, Platform::String^ entryPoint, const Platform::Array<MacroEntry^>^ macros);
		void Initialize(IBuffer^ data);
	internal:
		std::vector<_computeShaderDesc> m_pipelineStates;
		Microsoft::WRL::ComPtr<ID3DBlob> m_byteCode;
	};
}
