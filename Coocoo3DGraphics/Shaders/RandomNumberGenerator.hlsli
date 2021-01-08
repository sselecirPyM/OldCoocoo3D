// Ref: http://www.reedbeta.com/blog/quick-and-easy-gpu-random-numbers-in-d3d11/
namespace RNG
{
	static const float RANDOM_NUMBER_PI = 3.141592653589793238;
	uint RandomSeed(uint seed)
	{
		// Thomas Wang hash 
		// Ref: http://www.burtleburtle.net/bob/hash/integer.html
		seed = (seed ^ 61) ^ (seed >> 16);
		seed *= 9;
		seed = seed ^ (seed >> 4);
		seed *= 0x27d4eb2d;
		seed = seed ^ (seed >> 15);
		return seed;
	}

	// Generate a random 32-bit integer
	uint Random(inout uint state)
	{
		// Xorshift algorithm from George Marsaglia's paper.
		state ^= (state << 13);
		state ^= (state >> 17);
		state ^= (state << 5);
		return state;
	}

	// Generate a random float in the range [0.0f, 1.0f)
	float Random01(inout uint state)
	{
		return asfloat(0x3f800000 | Random(state) >> 9) - 1.0;
	}

	// Generate a random float in the range [0.0f, 1.0f]
	float Random01inclusive(inout uint state)
	{
		return Random(state) / float(0xffffffff);
	}

	// Generate a random integer in the range [lower, upper]
	uint Random(inout uint state, uint lower, uint upper)
	{
		return lower + uint(float(upper - lower + 1) * Random01(state));
	}

	//Generate normal distribution random float ~N(0,1)
	float NDRandom(inout uint state)
	{
		float R = sqrt(-2 * log(1 - Random01(state)));
		float theta = 2 * 3.141592653589793238 * Random01(state);
		return R * cos(theta);
	}

	float2 Hammersley(uint Index, uint NumSamples, uint2 Random)
	{
		float E1 = frac((float)Index / NumSamples + float(Random.x & 0xffff) / (1 << 16));
		float E2 = float(reversebits(Index) ^ Random.y) * 2.3283064365386963e-10;
		return float2(E1, E2);
	}

	float3 HammersleySampleCos(float2 Xi)
	{
		float phi = 2 * RANDOM_NUMBER_PI * Xi.x;

		float cosTheta = sqrt(Xi.y);
		float sinTheta = sqrt(1 - cosTheta * cosTheta);

		float3 H;
		H.x = sinTheta * cos(phi);
		H.y = sinTheta * sin(phi);
		H.z = cosTheta;

		return H;
	}
}