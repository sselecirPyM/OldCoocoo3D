#pragma once
namespace Coocoo3DGraphics
{
	public ref class WICFactory sealed
	{
	public:
		WICFactory();
	internal:
		IWICImagingFactory2* GetWicImagingFactory() const { return m_wicFactory.Get(); }
		Microsoft::WRL::ComPtr<IWICImagingFactory2>	m_wicFactory;
	};
}