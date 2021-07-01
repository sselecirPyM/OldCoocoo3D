#pragma once
#include "GraphicsConstance.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class ReadBackTexture2D sealed
	{
	public:
		void Reload(int width, int height, int bytesPerPixel);
		void GetDataTolocal(int index);
		Platform::Array<byte>^ GetRaw(int index);
		void GetRaw(int index, const Platform::Array<byte>^ bitmapData);
		int GetWidth()
		{
			return m_width;
		}
		int GetHeight()
		{
			return m_height;
		}
		virtual ~ReadBackTexture2D();
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource> m_textureReadBack[c_frameCount] = {};
		byte* m_mappedData = nullptr;
		byte* m_localData = nullptr;
		UINT m_width;
		UINT m_height;
		UINT m_bytesPerPixel;
		UINT m_rowPitch;
	};
}
