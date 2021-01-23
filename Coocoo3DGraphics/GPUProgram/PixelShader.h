#pragma once
#include "ShaderMacro.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class PixelShader sealed
	{
	public:
		bool CompileInitialize1(IBuffer^ file1, Platform::String^ entryPoint, ShaderMacro macro);
		void Initialize(IBuffer^ data);
		virtual ~PixelShader();
	internal:
		Microsoft::WRL::ComPtr<ID3DBlob> byteCode;
	};
}

