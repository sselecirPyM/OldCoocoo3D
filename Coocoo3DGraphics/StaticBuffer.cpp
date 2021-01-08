#include "pch.h"
#include "StaticBuffer.h"
using namespace Coocoo3DGraphics;

void StaticBuffer::Reload(const Platform::Array<int>^ data)
{
	m_bufferData = ref new Platform::Array<byte> (data->Length * 4);
	memcpy(m_bufferData->begin(), data->begin(), data->Length * 4);
}

StaticBuffer::StaticBuffer()
{
	m_stride = 64;
}

void StaticBuffer::ReleaseUploadHeapResource()
{
	m_bufferUpload.Reset();
}
