using UnityEngine;

// Yetenek / akış yazılarını kapatır (CombatCallout çağrılarının yerine geçen boş sınıf).
public static class NoCallout
{
    public static void Popup(Vector3 position, string label, Color color, float scale = 1f, int sortingLayer = 0, int sortingOrder = 600) { }
    public static void PopupAbove(Component target, string label, Color color, float scale = 1f, float extraHeight = 0.35f) { }
}
