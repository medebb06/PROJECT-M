// Sprite'ı, SpriteRenderer.color renginde TAM DOLU bir silüete çevirir
// (SpriteRenderer.color = beyaz ise tam beyaz flaş).
//
// Unity'nin kendi "Sprites/Default" shader'ının birebir kopyasıdır;
// tek fark fragment kısmında RGB'nin texture yerine renkten gelmesi.
// Bu yüzden flipX, sprite atlas, instancing ve URP 2D ile aynı şekilde
// çalışır. Işıklandırmadan (2D Light) etkilenmez, yani flaş her zaman parlak.
//
// KULLANIM: Bu dosyayı Assets/Resources/ klasörüne koy.
// EnemyController 'Custom/SpriteWhiteFlash' adıyla kendisi bulur.
Shader "Custom/SpriteWhiteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFlashFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 SpriteFlashFrag(v2f IN) : SV_Target
            {
                // Şekil (alpha) sprite'tan, renk tamamen SpriteRenderer.color'dan.
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                c.rgb = IN.color.rgb * c.a;
                return c;
            }
        ENDCG
        }
    }
}
