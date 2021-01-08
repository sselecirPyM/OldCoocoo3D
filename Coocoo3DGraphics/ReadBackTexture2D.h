#pragma once
#include "WICFactory.h"
#include "DeviceResources.h"
namespace Coocoo3DGraphics
{
	using namespace Windows::Storage::Streams;
	public ref class ReadBackTexture2D sealed
	{
	public:
		void Reload(int width, int height, int bytesPerPixel);
		void GetDataTolocal(int index);
		Platform::Array<byte>^ EncodePNG(WICFactory^ wicFactory,int index);
		virtual ~ReadBackTexture2D();
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource> m_textureReadBack[c_frameCount] = {};
		byte* m_mappedData = nullptr;
		byte* m_localData = nullptr;
		UINT m_width=0;
		UINT m_height=0;
		UINT m_bytesPerPixel;
		UINT m_rowPitch;
	};
}
