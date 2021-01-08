#pragma once
#include "DeviceResources.h"
#include "GraphicsObjectStatus.h"
#include "GraphicsSignature.h"
#include "ShaderMacro.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class ComputePO sealed
	{
	public:
		property GraphicsObjectStatus Status;
		//使用Upload上传GPU
		bool CompileReload1(IBuffer^ file1, Platform::String^ entryPoint, ShaderMacro macro);
		void Reload(DeviceResources^ deviceResources,GraphicsSignature^ rootSignature, IBuffer^ data);
		bool Upload(DeviceResources^ deviceResources, GraphicsSignature^ rootSignature);
		void Reload(IBuffer^ data);
	internal:
		Microsoft::WRL::ComPtr<ID3D12PipelineState> m_pipelineState;
		Microsoft::WRL::ComPtr<ID3DBlob> byteCode;
	};
}
