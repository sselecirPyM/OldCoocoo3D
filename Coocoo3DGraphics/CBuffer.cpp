#include "pch.h"
#include "CBuffer.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

void CBuffer::Unload()
{
	m_constantBuffer.Reset();
	m_constantBufferUploads.Reset();
	lastUpdateIndex = 0;
}

D3D12_GPU_VIRTUAL_ADDRESS CBuffer::GetCurrentVirtualAddress()
{
	if (Mutable)
		return m_constantBuffer->GetGPUVirtualAddress() + m_size * lastUpdateIndex;
	else
		return m_constantBuffer->GetGPUVirtualAddress();
}
