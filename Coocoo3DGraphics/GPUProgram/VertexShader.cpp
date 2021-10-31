#include "pch.h"
#include "VertexShader.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;

void VertexShader::Initialize(const Platform::Array<byte>^ data)
{
	D3DCreateBlob(data->Length, &byteCode);
	memcpy(byteCode->GetBufferPointer(), data->begin(), data->Length);
}
