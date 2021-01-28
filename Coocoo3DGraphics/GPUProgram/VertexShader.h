#pragma once
#include "ShaderMacro.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class VertexShader sealed
	{
	public:
		bool CompileInitialize1(IBuffer^ file1, Platform::String^ entryPoint, const Platform::Array<MacroEntry^>^ macros);
		void Initialize(IBuffer^ data);
	internal:
		Microsoft::WRL::ComPtr<ID3DBlob> byteCode;
	};
}
