#pragma once
#include "ShaderMacro.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class GeometryShader sealed
	{
	public:
		bool CompileReload1(IBuffer^ file1, Platform::String^ entryPoint, ShaderMacro macro);
		void Reload(IBuffer^ data);
	internal:
		Microsoft::WRL::ComPtr<ID3DBlob> byteCode;
	};
}
