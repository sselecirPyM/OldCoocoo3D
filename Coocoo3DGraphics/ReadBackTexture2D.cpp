#include "pch.h"
#include "ReadBackTexture2D.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

void ReadBackTexture2D::Reload(int width, int height, int bytesPerPixel)
{
	m_width = width;
	m_height = height;
	m_bytesPerPixel = bytesPerPixel;
	m_rowPitch = (m_width * bytesPerPixel + 255) & ~255;
	if (m_localData)free(m_localData);
	m_localData = (byte*)malloc(m_rowPitch * m_height * 3);
}

void ReadBackTexture2D::GetDataTolocal(int index)
{
	UINT dataLengrh = ((m_width + 63) & ~63) * m_height * m_bytesPerPixel;
	CD3DX12_RANGE readRange(0, dataLengrh);
	DX::ThrowIfFailed(m_textureReadBack[index]->Map(0, &readRange, reinterpret_cast<void**>(&m_mappedData)));
	memcpy(m_localData + dataLengrh * index, m_mappedData, dataLengrh);
	m_textureReadBack[index]->Unmap(0,nullptr);
}

Platform::Array<byte>^ Coocoo3DGraphics::ReadBackTexture2D::GetRaw(int index)
{
	UINT dataLengrh = ((m_width + 63) & ~63) * m_height * m_bytesPerPixel;

	Platform::Array<byte>^ bitmapData = ref new Platform::Array<byte>(dataLengrh);
	memcpy(bitmapData->begin(), m_localData + dataLengrh * index, dataLengrh);
	return bitmapData;
}

void Coocoo3DGraphics::ReadBackTexture2D::GetRaw(int index, const Platform::Array<byte>^ bitmapData)
{
	UINT dataLengrh = ((m_width + 63) & ~63) * m_height * m_bytesPerPixel;
	memcpy(bitmapData->begin(), m_localData + dataLengrh * index, dataLengrh);
}

ReadBackTexture2D::~ReadBackTexture2D()
{
	if (m_localData)free(m_localData);
	m_localData = nullptr;
}
