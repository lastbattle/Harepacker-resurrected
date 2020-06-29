#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float Angle = 0.0f;
float BlurAmount = 0.0f;

Texture2D SpriteTexture;

sampler2D SpriteTextureSampler = sampler_state
{
	Texture = <SpriteTexture>;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{	
	float4 result = 0;
	float samples = 8;
	float rad = Angle * 0.0174533f;
	float xOffset = cos(rad);
	float yOffset = sin(rad);

	float2 uv = input.TextureCoordinates;
	for (int i = 0; i < samples; i++)
	{
		uv.x = uv.x - BlurAmount * xOffset;
		uv.y = uv.y - BlurAmount * yOffset;
		result += tex2D(SpriteTextureSampler, uv);
	}

	result /= samples;

	return result * input.Color;
}

technique SpriteDrawing
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};