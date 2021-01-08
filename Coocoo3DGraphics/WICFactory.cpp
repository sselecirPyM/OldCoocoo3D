#include "pch.h"
#include "WICFactory.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;

WICFactory::WICFactory()
{
	DX::ThrowIfFailed(
		CoCreateInstance(
			CLSID_WICImagingFactory2,
			nullptr,
			CLSCTX_INPROC_SERVER,
			IID_PPV_ARGS(&m_wicFactory)
		)
	);
}
