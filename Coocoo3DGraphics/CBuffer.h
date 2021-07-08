#pragma once
namespace Coocoo3DGraphics
{
	public ref class CBuffer sealed
	{
	public:
		void Unload();
		property int m_size;
	internal:
		D3D12_GPU_VIRTUAL_ADDRESS GetCurrentVirtualAddress();
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_constantBufferUploads;
		Microsoft::WRL::ComPtr<ID3D12Resource>				m_constantBuffer;
		int lastUpdateIndex = 0;
		byte* m_mappedConstantBuffer = nullptr;
		bool Mutable;
	};
}