#pragma once
#include "GraphicsConstance.h"
namespace Coocoo3DGraphics
{
	public ref class MMDMeshAppend sealed
	{
	public:
		void Reload(int count);
	internal:
		static const UINT c_vertexStride = 12;

		Microsoft::WRL::ComPtr<ID3D12Resource> m_vertexBufferPos;
		Microsoft::WRL::ComPtr<ID3D12Resource> m_vertexBufferPosUpload;
		D3D12_VERTEX_BUFFER_VIEW m_vertexBufferPosViews;
		int lastUpdateIndexs=0;
		int m_posCount;
		int m_bufferSize;
	};
}
