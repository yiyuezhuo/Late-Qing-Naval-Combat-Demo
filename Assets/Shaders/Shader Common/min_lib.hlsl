
#ifndef MIN_LIB
#define MIN_LIB

static const float PI = 3.141592653589793238462643383279;

// Convert point on unit sphere to longitude and latitude
float2 pointToLongitudeLatitude(float3 p) {
	float longitude = atan2(p.x, -p.z);
	float latitude = asin(p.y);
	return float2(longitude, latitude);
}

// Scale longitude and latitude (given in radians) to range [0, 1]
float2 longitudeLatitudeToUV(float2 longLat) {
	float longitude = longLat[0]; // range [-PI, PI]
	float latitude = longLat[1]; // range [-PI/2, PI/2]
	
	float u = (longitude / PI + 1) * 0.5;
	float v = latitude / PI + 0.5;
	return float2(u,v);
}

// Convert point on unit sphere to uv texCoord
float2 pointToUV(float3 p) {
	float2 longLat = pointToLongitudeLatitude(p);
	return longitudeLatitudeToUV(longLat);
}

// Get point on sphere from long/lat (given in radians)
float3 longitudeLatitudeToPoint(float2 longLat) {
	float longitude = longLat[0];
	float latitude = longLat[1];

	float y = sin(latitude);
	float r = cos(latitude); // radius of 2d circle cut through sphere at 'y'
	float x = sin(longitude) * r;
	float z = -cos(longitude) * r;
	return float3(x,y,z);
}

float distanceBetweenPointsOnUnitSphere(float3 a, float3 b)
{
	return acos(saturate(dot(a, b)));
}

#endif