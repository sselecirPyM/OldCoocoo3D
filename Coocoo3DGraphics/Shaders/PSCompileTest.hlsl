RaytracingAccelerationStructure __scene : register(t0);


float4 main() : SV_Target
{
    // a. Configure
    RayQuery<RAY_FLAG_CULL_NON_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH> query;

    uint ray_flags = 0; // Any this ray requires in addition those above.
    uint ray_instance_mask = 0xffffffff;

    // b. Initialize  - hardwired here to deliver minimal sample code.
    RayDesc ray;
    ray.TMin = 1e-5f;
    ray.TMax = 1e10f;
    ray.Origin = float3(0,0,0);
    ray.Direction = float3(0,0,1);
    query.TraceRayInline(__scene, ray_flags,  ray_instance_mask, ray);

    // c. Cast 

    // Proceed() is where behind-the-scenes traversal happens, including the heaviest of any driver inlined code.
    // In this simplest of scenarios, Proceed() only needs to be called once rather than a loop.
    // Based on the template specialization above, traversal completion is guaranteed.

    query.Proceed();

    // d. Examine and act on the result of the traversal.

    if (query.CommittedStatus() == COMMITTED_TRIANGLE_HIT)
    {
        // TODO: Grab ray parameters & sample accordingly.

        /* ShadeMyTriangleHit(
            query.CommittedInstanceIndex(),
            query.CommittedPrimitiveIndex(),
            query.CommittedGeometryIndex(),
            query.CommittedRayT(),
            query.CommittedTriangleBarycentrics(),
            query.CommittedTriangleFrontFace() );*/

        return float4(0,1,0,1);
    }
    else
    {
        // COMMITTED_NOTHING. From template specialization above, COMMITTED_PROCEDURAL_PRIMITIVE can't happen so no need to check for that.

        // Miss shading - sample the environment.
        // Environment_Sample(query.WorldRayOrigin(), query.WorldRayDirection()); 

        return float4(0,0,1,1);
    }

    return float4(1,0,0,1);

}