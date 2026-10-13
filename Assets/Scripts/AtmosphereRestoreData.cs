using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Atmosfer kurulmadan önceki materyal ve sorting order'ları saklar.
/// Tools > Lighting > Remove Moonlit Atmosphere bunlarla eski hale döndürür.
/// </summary>
public class AtmosphereRestoreData : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public Renderer renderer;
        public Material material;
        public int sortingOrder;
    }

    public List<Entry> entries = new List<Entry>();
    public List<GameObject> createdObjects = new List<GameObject>();
    public List<Component> addedComponents = new List<Component>();

    public UnityEngine.Rendering.Universal.Light2D globalLight;
    public float globalLightIntensity = 1f;
    public Color globalLightColor = Color.white;
}
