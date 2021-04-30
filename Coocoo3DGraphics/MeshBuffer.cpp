#include "pch.h"
#include "MeshBuffer.h"
#include "DirectXHelper.h"

using namespace Coocoo3DGraphics;

void MeshBuffer::StateTransition(ID3D12GraphicsCommandList* commandList, D3D12_RESOURCE_STATES state)
{
	if (m_prevState != state)
		commandList->ResourceBarrier(1, &CD3DX12_RESOURCE_BARRIER::Transition(m_buffer.Get(), m_prevState, state));
	m_prevState = state;
}
