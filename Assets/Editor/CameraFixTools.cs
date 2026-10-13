#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tools > Camera > Disable Duplicate Camera (CinemachineCamera üzerindeki fazladan Camera)
/// Sahnede 'CinemachineCamera' objesinin üzerinde de bir Camera + PixelPerfectCamera + URP kamera verisi var.
/// O Camera açık ve derinliği (0) Main Camera'dan (-1) büyük: Main Camera'nın üstüne çiziliyor.
/// Yani ekranda gördüğün şey Main Camera DEĞİL; bu yüzden Cinemachine lensi / koşu uzaklaşması hiçbir şey değiştirmiyordu.
/// Bu araç o fazla Camera'yı ve Pixel Perfect'i KAPATIR (silmez, Undo ile geri alınır).
/// Görüş boyutu bundan sonra CinemachineCamera > Lens > Ortho Size'dan gelir.
/// </summary>
public static class CameraFixTools
{
    [MenuItem("Tools/Camera/Disable Duplicate Camera (CinemachineCamera üstündeki fazla kamera)")]
    private static void Fix()
    {
        int n = 0;
        var sb = new System.Text.StringBuilder("Kamera düzeltmesi:\n");

        foreach (CinemachineCamera v in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Camera cam = v.GetComponent<Camera>();
            PixelPerfectCamera ppc = v.GetComponent<PixelPerfectCamera>();

            if (ppc != null && ppc.enabled)
            {
                Undo.RecordObject(ppc, "Disable PPC");
                ppc.enabled = false;
                sb.AppendLine($"  {v.name}: PixelPerfectCamera kapatıldı");
                n++;
            }

            if (cam != null && cam.enabled)
            {
                Undo.RecordObject(cam, "Disable duplicate camera");
                cam.enabled = false;
                sb.AppendLine($"  {v.name}: fazladan Camera kapatıldı (eski ortho {cam.orthographicSize:0.0})");
                n++;
            }

            sb.AppendLine($"  {v.name}: Lens Ortho Size = {v.Lens.OrthographicSize:0.0}  (görüş alanı bundan gelir)");
        }

        if (n > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

        Debug.Log(sb.Append(n == 0 ? "  Kapatılacak bir şey bulunamadı." : "  Sahneyi kaydet (Ctrl+S). Çerçeve değiştiyse CinemachineCamera > Lens > Ortho Size ile ayarla.").ToString());
    }
}
#endif
