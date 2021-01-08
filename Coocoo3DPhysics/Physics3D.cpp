#include "pch.h"
#include "Physics3D.h"

using namespace Coocoo3DPhysics;

void Physics3D::Init()
{
	m_sdkRef->Init();
//#define PVD_HOST "127.0.0.1"
//
//	m_foundation = PxCreateFoundation(PX_PHYSICS_VERSION, gAllocator, gErrorCallback);
//
//	m_pvd = PxCreatePvd(*m_foundation);
//	PxPvdTransport* transport = PxDefaultPvdSocketTransportCreate(PVD_HOST, 5425, 10);
//	m_pvd->connect(*transport, PxPvdInstrumentationFlag::eALL);
//	m_physics = PxCreatePhysics(PX_PHYSICS_VERSION, *m_foundation, PxTolerancesScale(), true, m_pvd);
//	PxInitExtensions(*m_physics, m_pvd);
//
//	m_dispatcher = PxDefaultCpuDispatcherCreate(4);

	m_loaded = true;
}

void Physics3D::release()
{
	//if (!m_loaded)return;
	//m_physics->release();
	//m_foundation->release();
}

Physics3D::~Physics3D()
{
	//if (!m_loaded)return;
	//m_physics->release();
	//m_foundation->release();
}
