#pragma once
namespace Coocoo3DGraphics
{
	public ref class StaticBuffer sealed
	{
	public:
		property Platform::Array<byte>^ m_bufferData;
		property UINT m_stride;
		void Reload(const Platform::Array<int>^ data);
		StaticBuffer();
		void ReleaseUploadHeapResource();
	internal:
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_buffer;
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_bufferUpload;
		UINT m_heapRefIndex;
	};
}
