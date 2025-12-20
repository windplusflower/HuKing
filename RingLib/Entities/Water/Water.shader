Shader "RingLib/Water"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NumSegments ("NumSegments", Int) = 0
        _SegmentOriginalHeight ("SegmentOriginalHeight", Float) = 0
        _SegmentOriginalBottom ("SegmentOriginalBottom", Float) = 0
        _RenderingDampening ("RenderingDampening", Float) = 0
        _EmissionIntensity ("Emission Intensity", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            #define NumSegmentsMax 512
            int _NumSegments;
            float _SegmentOriginalHeight;
            uniform float _SegmentX[NumSegmentsMax];
            uniform float _SegmentTop[NumSegmentsMax];
            float _SegmentOriginalBottom;
            float _RenderingDampening;
            float _EmissionIntensity;

            float catmullRom(float t, float p0, float p1, float p2, float p3)
            {
                float v0 = (p2 - p0) * 0.5;
                float v1 = (p3 - p1) * 0.5;
                float t2 = t * t;
                float t3 = t * t2;
                return (2 * p1 - 2 * p2 + v0 + v1) * t3
                    + (-3 * p1 + 3 * p2 - 2 * v0 - v1) * t2 + v0 * t + p1;
            }

            float nonuniformScale(float la, float ta, float lb)
            {
                float k = lb / la;
                float tb = pow(ta, (k - 1) * _RenderingDampening + 1);
                return tb;
            }

            v2f vert (appdata v)
            {
                _NumSegments = min(_NumSegments, NumSegmentsMax);
                if (_NumSegments > 0)
                {
                    float2 worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                    int index;
                    if (_SegmentX[0] >= worldPos.x)
                    {
                        index = -1;
                    }
                    else
                    {
                        for (int i = NumSegmentsMax / 2; i > 0; i /= 2)
                        {
                            if (index + i < _NumSegments && _SegmentX[index + i] < worldPos.x)
                            {
                                index += i;
                            }
                        }
                    }

                    int i0 = index - 1;
                    int i1 = index;
                    int i2 = index + 1;
                    int i3 = index + 2;
                    float newTop;
                    if (i0 >= 0 && i3 < _NumSegments)
                    {
                        float x0 = _SegmentX[i0];
                        float y0 = _SegmentTop[i0];
                        float x1 = _SegmentX[i1];
                        float y1 = _SegmentTop[i1];
                        float x2 = _SegmentX[i2];
                        float y2 = _SegmentTop[i2];
                        float x3 = _SegmentX[i3];
                        float y3 = _SegmentTop[i3];
                        float t = (worldPos.x - x1) / (x2 - x1);
                        newTop = catmullRom(t, y0, y1, y2, y3);
                    }
                    else if (i1 >= 0 && i2 < _NumSegments)
                    {
                        float x1 = _SegmentX[i1];
                        float y1 = _SegmentTop[i1];
                        float x2 = _SegmentX[i2];
                        float y2 = _SegmentTop[i2];
                        float t = (worldPos.x - x1) / (x2 - x1);
                        newTop = lerp(y1, y2, t);
                    }
                    else if (i1 >= 0)
                    {
                        newTop = _SegmentTop[i1];
                    }
                    else
                    {
                        newTop = _SegmentTop[i2];
                    }

                    float t = (worldPos.y - _SegmentOriginalBottom) / _SegmentOriginalHeight;
                    t = clamp(t, 0, 1);
                    t = nonuniformScale(_SegmentOriginalHeight, t, newTop - _SegmentOriginalBottom);
                    float newY = lerp(_SegmentOriginalBottom, newTop, t);
                    v.vertex.y = (v.vertex.y - worldPos.y) + newY;
                }
                
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                col.rgb *= _EmissionIntensity;
                return col;
            }
            ENDCG
        }
    }
}
