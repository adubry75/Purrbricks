using System;
using System.Collections.Generic;
using Newtonsoft.Json;

[Serializable]
public sealed class NineLivesModel
{
    public int Version = 1;
    public bool IntroGranted;
    public int TotalXp;
    public int PurchasedMask;
    public string EquippedCapstone = "";
    public int StarterPowerup;
    public bool MigrationCompleted;
    public Dictionary<string, int> CreditedStars = new Dictionary<string, int>();
    public const int MaximumXp = 10290;
    [JsonIgnore] public int LifetimePoints { get { if (!IntroGranted) return 0; int points = 1, remaining = Math.Max(0, TotalXp); while (points < 15 && remaining >= Cost(points)) { remaining -= Cost(points); points++; } return points; } }
    [JsonIgnore] public int UnspentPoints { get { int count = 0; for (int i = 0; i < 15; i++) if ((PurchasedMask & (1 << i)) != 0) count++; return Math.Max(0, LifetimePoints - count); } }
    [JsonIgnore] public int NextPointCost { get { return LifetimePoints >= 15 ? 0 : Cost(Math.Max(1, LifetimePoints)); } }
    [JsonIgnore] public int XpTowardNextPoint { get { if (!IntroGranted || LifetimePoints >= 15) return 0; int xp = TotalXp; for (int i = 1; i < LifetimePoints; i++) xp -= Cost(i); return Math.Max(0, xp); } }
    static int Cost(int earned) { return 150 + (earned - 1) * 90; }
    public bool Has(string id) { var node = NineLivesCatalog.Find(id); return node != null && (PurchasedMask & (1 << node.Index)) != 0; }
    public bool CanPurchase(string id, out string reason)
    {
        var node = NineLivesCatalog.Find(id);
        if (node == null) { reason = "Unknown upgrade."; return false; }
        if (Has(id)) { reason = "Already purchased."; return false; }
        if (!Requirements(node)) { reason = node.IsCapstone ? "Requires the previous upgrade and 8 total skill points earned (spent points count)." : "Purchase the connected prerequisites first."; return false; }
        if (UnspentPoints < 1) { reason = "Requires 1 skill point."; return false; }
        reason = ""; return true;
    }
    bool Requirements(NineLivesNode node)
    {
        string branch = node.Id.Substring(0, 1);
        switch (node.Index % 5) { case 0: return true; case 1: case 2: return Has(branch + "1"); case 3: return Has(branch + "2") && Has(branch + "3"); default: return Has(branch + "4") && LifetimePoints >= 8; }
    }
    public bool Purchase(string id) { string reason; if (!CanPurchase(id, out reason)) return false; var node = NineLivesCatalog.Find(id); PurchasedMask |= 1 << node.Index; if (node.IsCapstone && string.IsNullOrEmpty(EquippedCapstone)) EquippedCapstone = id; return true; }
    public void Respec() { PurchasedMask = 0; EquippedCapstone = ""; }
    public bool EquipCapstone(string id) { var node = NineLivesCatalog.Find(id); if (node == null || !node.IsCapstone || !Has(id)) return false; EquippedCapstone = id; return true; }
    public void AddXp(int xp) { if (!IntroGranted || xp <= 0) return; TotalXp = (int)Math.Min(MaximumXp, (long)Math.Max(0, TotalXp) + xp); }
    public bool CompleteLevel(string levelId, string introId, int stars)
    {
        if (string.IsNullOrEmpty(levelId)) return false;
        EnsureLedger(); stars = Math.Max(0, Math.Min(3, stars));
        if (!IntroGranted)
        {
            if (levelId != introId) return false;
            IntroGranted = true; TotalXp = 0; CreditedStars[levelId] = stars; return true;
        }
        int old; bool cleared = CreditedStars.TryGetValue(levelId, out old);
        AddXp(30 + (cleared ? 0 : 60) + 5 * Math.Max(0, stars - old));
        CreditedStars[levelId] = Math.Max(old, stars); return false;
    }
    public void Migrate(string introId, Dictionary<string, int> savedStars)
    {
        if (MigrationCompleted) return;
        EnsureLedger(); MigrationCompleted = true;
        if (savedStars == null) return;
        foreach (var entry in savedStars)
        {
            if (string.IsNullOrEmpty(entry.Key) || entry.Value <= 0) continue;
            if (!IntroGranted) { IntroGranted = true; TotalXp = 0; }
            int stars = Math.Min(3, entry.Value), old;
            bool credited = CreditedStars.TryGetValue(entry.Key, out old);
            if (entry.Key != introId) AddXp((credited ? 0 : 110) + 5 * Math.Max(0, stars - old));
            CreditedStars[entry.Key] = Math.Max(old, stars);
        }
        // The intro reward is consumed even when historical evidence proves only a later level.
        if (IntroGranted && !string.IsNullOrEmpty(introId) && !CreditedStars.ContainsKey(introId)) CreditedStars[introId] = 0;
    }
    void EnsureLedger() { if (CreditedStars == null) CreditedStars = new Dictionary<string, int>(); }
    public void Normalize()
    {
        Version = 1; TotalXp = IntroGranted ? Math.Max(0, Math.Min(MaximumXp, TotalXp)) : 0;
        EnsureLedger(); var clean = new Dictionary<string, int>(); foreach (var entry in CreditedStars) if (!string.IsNullOrEmpty(entry.Key)) clean[entry.Key] = Math.Max(0, Math.Min(3, entry.Value)); CreditedStars = clean;
        int original = PurchasedMask; PurchasedMask = 0;
        foreach (var node in NineLivesCatalog.Nodes) if ((original & (1 << node.Index)) != 0 && UnspentPoints > 0 && Requirements(node)) PurchasedMask |= 1 << node.Index;
        var equipped = NineLivesCatalog.Find(EquippedCapstone); if (equipped == null || !equipped.IsCapstone || !Has(EquippedCapstone)) EquippedCapstone = "";
        if (StarterPowerup != 0 && StarterPowerup != 2 && StarterPowerup != 5) StarterPowerup = 0;
    }
}
