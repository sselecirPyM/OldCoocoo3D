#pragma once
#include "DeviceResources.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class Uploader sealed
	{
	public:
		void Texture2D(IBuffer^ file1, bool srgb, bool generateMips);
		void Texture2DPure(int width, int height, Windows::Foundation::Numerics::float4 color);
		void TextureCube(int width, int height, const Platform::Array <IBuffer^>^ files);
		void TextureCubePure(int width, int height, const Platform::Array<Windows::Foundation::Numerics::float4>^ color);
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource> m_buffer;

		UINT m_width;
		UINT m_height;
		UINT m_mipLevels;
		DXGI_FORMAT m_format;

		std::vector<byte> m_data;
	};
}
