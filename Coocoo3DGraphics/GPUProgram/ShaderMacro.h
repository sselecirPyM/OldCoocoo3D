#pragma once
namespace Coocoo3DGraphics
{
	public ref class MacroEntry sealed
	{
	public:
		MacroEntry(Platform::String^ name, Platform::String^ value);
	internal:
		Platform::String^ Name;
		Platform::String^ Value;

	};
}