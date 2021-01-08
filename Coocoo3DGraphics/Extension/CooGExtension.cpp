#include "pch.h"
#include "CooGExtension.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
inline CD3DX12_GPU_DESCRIPTOR_HANDLE& _select(GraphicsObjectStatus status, CD3DX12_GPU_DESCRIPTOR_HANDLE& tex1, CD3DX12_GPU_DESCRIPTOR_HANDLE& loading, CD3DX12_GPU_DESCRIPTOR_HANDLE& error)
{
	if (status == GraphicsObjectStatus::loaded)
		return tex1;
	else if (status == GraphicsObjectStatus::loading)
		return loading;
	else
		return error;
}

void CooGExtension::SetSRVTexture2(GraphicsContext^ context, Texture2D^ tex1, Texture2D^ tex2, int startSlot, Texture2D^ loading, Texture2D^ error)
{
	auto d3dDevice = context->m_deviceResources->GetD3DDevice();
	UINT incrementSize = d3dDevice->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
	D3D12_GPU_DESCRIPTOR_HANDLE heapStart = context->m_deviceResources->m_cbvSrvUavHeap->GetGPUDescriptorHandleForHeapStart();

	CD3DX12_GPU_DESCRIPTOR_HANDLE loadingTexHandle(heapStart, loading->m_heapRefIndex, incrementSize);
	CD3DX12_GPU_DESCRIPTOR_HANDLE errorTexHandle(heapStart, error->m_heapRefIndex, incrementSize);
	if (tex1 != nullptr)
	{
		CD3DX12_GPU_DESCRIPTOR_HANDLE tex1Handle(heapStart, tex1->m_heapRefIndex, incrementSize);
		auto _tex1 = _select(tex1->Status, tex1Handle, loadingTexHandle, errorTexHandle);
		context->m_commandList->SetGraphicsRootDescriptorTable(startSlot, _tex1);
	}
	if (tex2 != nullptr)
	{
		CD3DX12_GPU_DESCRIPTOR_HANDLE tex2Handle(heapStart, tex2->m_heapRefIndex, incrementSize);
		auto _tex2 = _select(tex2->Status, tex2Handle, loadingTexHandle, errorTexHandle);
		context->m_commandList->SetGraphicsRootDescriptorTable(startSlot + 1, _tex2);
	}

}

void CooGExtension::SetCBVBuffer3(GraphicsContext^ context, ConstantBuffer^ buffer1, ConstantBuffer^ buffer2, ConstantBuffer^ buffer3, int startSlot)
{
	context->m_commandList->SetGraphicsRootConstantBufferView(startSlot, buffer1->GetCurrentVirtualAddress());
	context->m_commandList->SetGraphicsRootConstantBufferView(startSlot + 1, buffer2->GetCurrentVirtualAddress());
	context->m_commandList->SetGraphicsRootConstantBufferView(startSlot + 2, buffer3->GetCurrentVirtualAddress());
}
