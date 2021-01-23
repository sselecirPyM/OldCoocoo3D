#include "pch.h"
#include "SBuffer.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

void SBuffer::Unload()
{
	lastUpdateIndex = 0;
}

D3D12_GPU_VIRTUAL_ADDRESS SBuffer::GetCurrentVirtualAddress()
{
	return m_constantBuffer->GetGPUVirtualAddress();
}
