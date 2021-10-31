#pragma once
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class VertexShader sealed
	{
	public:
		void Initialize(const Platform::Array<byte>^ data);
	internal:
		Microsoft::WRL::ComPtr<ID3DBlob> byteCode;
	};
}
