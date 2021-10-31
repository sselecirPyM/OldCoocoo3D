#pragma once
#include "GraphicsConstance.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class ReadBackTexture2D sealed
	{
	public:
		void Reload(int width, int height, int bytesPerPixel);
		void GetRaw(int index, const Platform::Array<byte>^ bitmapData);
		int GetWidth()
		{
			return width;
		}
		int GetHeight()
		{
			return height;
		}
		virtual ~ReadBackTexture2D();
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource> m_textureReadBack[c_frameCount] = {};
		byte* m_mappedData = nullptr;
		UINT width;
		UINT height;
		UINT m_bytesPerPixel;
	};
}
