using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun o koşudaki charm'ları. Aynı charm tekrar alınırsa İSTİFLENİR
/// (stacks artar) ve etkisi güçlenir. Efektler envantere girerken
/// kopyalanır, koşu bitince Clear() ile tamamen temizlenir.
/// </summary>
public class CharmInventory
{
    public class Entry
    {
        public CharmDefinition definition;
        public int stacks;
        public CharmEffect effect;
        public CharmContext context;
    }

    private readonly List<Entry> entries = new List<Entry>();

    private readonly PlayerStats stats;
    private readonly Health playerHealth;
    private readonly PlayerController playerController;
    private readonly PlayerPosture posture;

    public IReadOnlyList<Entry> Entries => entries;

    public event System.Action Changed;

    public CharmInventory(GameObject player)
    {
        stats = player.GetComponent<PlayerStats>();

        if (stats == null)
            stats = player.AddComponent<PlayerStats>();

        playerHealth = player.GetComponent<Health>();
        playerController = player.GetComponent<PlayerController>();
        posture = player.GetComponent<PlayerPosture>();

        // Davranış charm'larının saat/dash takibi.
        CharmRunner.Ensure(player);
    }

    public int GetStacks(CharmDefinition definition)
    {
        Entry e = Find(definition);

        return e != null ? e.stacks : 0;
    }

    // maxStacks 0 ise sınırsız. Ön koşul (requiresAnyOf) da kontrol edilir.
    public bool CanAdd(CharmDefinition definition)
    {
        if (definition == null)
            return false;

        if (!MeetsRequirements(definition))
            return false;

        return
            definition.maxStacks <= 0 ||
            GetStacks(definition) < definition.maxStacks;
    }

    public bool MeetsRequirements(CharmDefinition definition)
    {
        List<CharmDefinition> req = definition.requiresAnyOf;

        if (req == null || req.Count == 0)
            return true;

        for (int i = 0; i < req.Count; i++)
        {
            if (req[i] != null && GetStacks(req[i]) > 0)
                return true;
        }

        return false;
    }

    public bool Add(CharmDefinition definition)
    {
        if (!CanAdd(definition))
            return false;

        Entry e = Find(definition);

        if (e == null)
        {
            e = new Entry
            {
                definition = definition,
                stacks = 0,
                effect =
                    definition.effect != null
                        ? Object.Instantiate(definition.effect)
                        : null
            };

            e.context =
                new CharmContext
                {
                    stats = stats,
                    playerHealth = playerHealth,
                    player = playerController,
                    posture = posture,
                    owner = e
                };

            entries.Add(e);
        }

        e.stacks++;

        if (e.effect != null)
            e.effect.Apply(e.context, e.stacks);

        Changed?.Invoke();

        return true;
    }

    // Koşu bitti / yeniden başlıyor: her şeyi geri al.
    public void Clear()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];

            if (e.effect != null)
            {
                e.effect.Remove(e.context);

                Object.Destroy(e.effect);
            }
        }

        entries.Clear();

        Changed?.Invoke();
    }

    private Entry Find(CharmDefinition definition)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].definition == definition)
                return entries[i];
        }

        return null;
    }
}
