#include "pch.h"
#include "CBuffer.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

void CBuffer::Unload()
{
	m_constantBuffer.Reset();
	lastUpdateIndex = 0;
}

D3D12_GPU_VIRTUAL_ADDRESS CBuffer::GetCurrentVirtualAddress()
{
	return m_constantBuffer->GetGPUVirtualAddress() + m_size * lastUpdateIndex;
}
