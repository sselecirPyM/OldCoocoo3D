#include "pch.h"
#include "DirectXHelper.h"
#include "ComputeShader.h"
using namespace Coocoo3DGraphics;

void ComputeShader::Initialize(const Platform::Array<byte>^ data)
{
	D3DCreateBlob(data->Length, &m_byteCode);
	memcpy(m_byteCode->GetBufferPointer(), data->begin(), data->Length);
}
