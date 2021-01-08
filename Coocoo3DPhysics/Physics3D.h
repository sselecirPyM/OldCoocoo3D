#pragma once
#include "IPhysicsAPI.h"
namespace Coocoo3DPhysics
{
	public ref class Physics3D sealed
	{
	public:
		void Init();
		void release();
		virtual ~Physics3D();
	internal:
		bool m_loaded = false;
		std::shared_ptr<IPhysicsAPI> m_sdkRef;
		//physx::PxDefaultAllocator		gAllocator;
		//physx::PxDefaultErrorCallback	gErrorCallback;

		//physx::PxFoundation*			m_foundation = NULL;
		//physx::PxPhysics*				m_physics = NULL;
		//physx::PxPvd* m_pvd = nullptr;

		//physx::PxDefaultCpuDispatcher*	m_dispatcher = NULL;

		//physx::PxCudaContextManager* gCudaContextManager = NULL;
	};
}
