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

Platform::Array<byte>^ ReadBackTexture2D::EncodePNG(WICFactory^ wicFactory, int index)
{
	UINT dataLengrh = ((m_width + 63) & ~63) * m_height * m_bytesPerPixel;
	HGLOBAL HGlobalImage = GlobalAlloc(GMEM_ZEROINIT | GMEM_MOVEABLE, dataLengrh);

	ComPtr<IStream> memStream = nullptr;
	DX::ThrowIfFailed(CreateStreamOnHGlobal(HGlobalImage, true, &memStream));
	DX::ThrowIfFailed(memStream->Seek(LARGE_INTEGER{ 0,0 }, STREAM_SEEK_SET, nullptr));
	WICRect rect = {};
	rect.Width = m_width;
	rect.Height = m_height;
	auto factory = wicFactory->GetWicImagingFactory();

	ComPtr<IWICBitmap> bitmap = nullptr;
	ComPtr<IWICBitmapEncoder> encoder = nullptr;
	ComPtr<IWICBitmapFrameEncode> frame1 = nullptr;
	ComPtr<IPropertyBag2> propertyBag = nullptr;
	DX::ThrowIfFailed(factory->CreateBitmapFromMemory(m_width, m_height, GUID_WICPixelFormat32bppBGRA, ((m_width + 63) & ~63) * m_bytesPerPixel, ((m_width + 63) & ~63) * m_height * m_bytesPerPixel, m_localData + dataLengrh * index, &bitmap));
	DX::ThrowIfFailed(factory->CreateEncoder(GUID_ContainerFormatPng, nullptr, &encoder));
	DX::ThrowIfFailed(encoder->Initialize(memStream.Get(), WICBitmapEncoderNoCache));
	DX::ThrowIfFailed(encoder->CreateNewFrame(&frame1, &propertyBag));
	DX::ThrowIfFailed(frame1->Initialize(propertyBag.Get()));
	DX::ThrowIfFailed(frame1->SetSize(m_width, m_height));
	DX::ThrowIfFailed(frame1->WriteSource(bitmap.Get(), &rect));
	DX::ThrowIfFailed(frame1->Commit());
	DX::ThrowIfFailed(encoder->Commit());

	ULARGE_INTEGER curPos = {};
	ULARGE_INTEGER startPos = {};

	DX::ThrowIfFailed(memStream->Seek(LARGE_INTEGER{}, STREAM_SEEK_CUR, &curPos));
	DX::ThrowIfFailed(memStream->Seek(LARGE_INTEGER{}, STREAM_SEEK_SET, &startPos));
	ULONG64 imgSize = curPos.QuadPart - startPos.QuadPart;
	Platform::Array<byte>^ bitmapData = ref new Platform::Array<byte>(imgSize);
	memStream->Read(bitmapData->begin(), (ULONG)imgSize, nullptr);
	GlobalUnlock(HGlobalImage);
	return bitmapData;
}

ReadBackTexture2D::~ReadBackTexture2D()
{
	if (m_localData)free(m_localData);
	m_localData = nullptr;
}
