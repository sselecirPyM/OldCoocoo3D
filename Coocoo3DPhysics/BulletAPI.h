#pragma once
#include "Physics3D.h"
#include "Bullet/Bullet.h"
namespace Coocoo3DPhysics
{
	public ref class BulletAPI sealed
	{
	public:
		static void SetAPIUsed(Physics3D^ physics3D);
	};
}