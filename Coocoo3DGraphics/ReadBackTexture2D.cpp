#include "pch.h"
#include "ReadBackTexture2D.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
using namespace Microsoft::WRL;

void ReadBackTexture2D::Reload(int width, int height, int bytesPerPixel)
{
	this->width = width;
	this->height = height;
	m_bytesPerPixel = bytesPerPixel;
}

void ReadBackTexture2D::GetRaw(int index, const Platform::Array<byte>^ bitmapData)
{
	UINT dataLengrh = ((width + 63) & ~63) * height * m_bytesPerPixel;
	CD3DX12_RANGE readRange(0, dataLengrh);
	DX::ThrowIfFailed(m_textureReadBack[index]->Map(0, &readRange, reinterpret_cast<void**>(&m_mappedData)));
	memcpy(bitmapData->begin(), m_mappedData, dataLengrh);
	m_textureReadBack[index]->Unmap(0, nullptr);
}

ReadBackTexture2D::~ReadBackTexture2D()
{
}
