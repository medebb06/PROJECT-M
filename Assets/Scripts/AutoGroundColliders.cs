using UnityEngine;

/// <summary>
/// OTOMATİK ZEMİN COLLIDER'I (işaretçi bileşen).
///
/// Bu bileşeni bir "zemin klasörüne" (ör. TestGrounds) ekle. Altındaki (her derinlikteki) her SpriteRenderer'lı
/// nesne için editör, sprite'ına TAM uyan collider'ı kendisi verir ve tutar:
///   - Dikdörtgen şekil (ya da Sliced / Tiled çizim) -> BoxCollider2D, renderer boyutuna göre.
///   - Üçgen, daire, serbest şekil -> PolygonCollider2D, sprite'ın fizik şekline göre.
///   - Sprite'ı değiştirirsen, boyutunu (Size) ya da Flip'i değiştirirsen collider yeniden hesaplanır.
///   - Scale / Rotation zaten collider'a da uygulanır; elle bir şey yapman gerekmez.
///   - Nesnenin katmanı Ground yapılır (oyuncunun zemin kontrolü ve eğimde kayma sadece Ground katmanını görür).
///
/// Bir nesneyi dışarıda bırakmak için adını "_" ile başlat. Eşitleme sadece edit modunda çalışır
/// (collider'lar sahneye kaydedilir; oyun sırasında ek yük yok). Kod: Assets/Editor/AutoGroundColliderSync.cs
/// </summary>
[DisallowMultipleComponent]
public class AutoGroundColliders : MonoBehaviour
{
    [Tooltip("Altındaki nesnelerin katmanını Ground yap.")]
    public bool forceGroundLayer = true;

    [Tooltip("Ground katmanının adı.")]
    public string groundLayerName = "Ground";

    [Tooltip("Dikdörtgen sayılacak şekil toleransı (sprite biriminin oranı).")]
    public float rectTolerance = 0.002f;
}
