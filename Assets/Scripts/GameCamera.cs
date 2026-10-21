using UnityEngine;

/// <summary>
/// Ekrana GERÇEKTEN çizen kamera.
/// Sahnede iki "Base" kamera var (Main Camera + CinemachineCamera objesindeki
/// kamera bileşeni, derinlik 0, SolidColor temizler): sonra çizilen öncekinin
/// üstüne biner. Camera.main bu yüzden yanlış lens (ortho) verip dünya→ekran
/// dönüşümünde çubukların kamerayla kaymasına yol açıyordu.
/// WorldToScreenPoint gibi işlemlerde Camera.main yerine bunu kullan.
/// </summary>
public static class GameCamera
{
    private static Camera cached;
    private static int cachedFrame = -1;

    public static Camera Get()
    {
        if (cachedFrame == Time.frameCount && cached != null)
            return cached;

        cachedFrame = Time.frameCount;

        Camera best = null;

        Camera[] all = Camera.allCameras;

        for (int i = 0; i < all.Length; i++)
        {
            Camera c = all[i];

            if (c == null || !c.enabled || c.targetTexture != null || !c.orthographic)
                continue;

            // Son çizilen (en yüksek derinlik) kazanır; eşitlikte Main Camera.
            if (
                best == null ||
                c.depth > best.depth ||
                (c.depth == best.depth && c == Camera.main)
            )
            {
                best = c;
            }
        }

        cached = best != null ? best : Camera.main;

        return cached;
    }
}
