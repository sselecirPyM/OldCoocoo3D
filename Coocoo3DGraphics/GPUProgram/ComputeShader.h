#pragma once
#include "Interoperation/InteroperationTypes.h"
#include "RootSignature.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class ComputeShader sealed
	{
	public:
		property GraphicsObjectStatus Status;
		void Initialize(const Platform::Array<byte>^ data);
	internal:
		std::map<ULONG, Microsoft::WRL::ComPtr<ID3D12PipelineState>>m_pipelineStates1;
		Microsoft::WRL::ComPtr<ID3DBlob> m_byteCode;
	};
}
