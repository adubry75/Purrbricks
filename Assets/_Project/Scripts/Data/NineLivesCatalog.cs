using System.Collections.Generic;

public sealed class NineLivesNode
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Branch { get; private set; }
    public int Index { get; private set; }
    public bool IsCapstone { get { return Index % 5 == 4; } }
    internal NineLivesNode(string id, string name, string description, string branch, int index) { Id = id; Name = name; Description = description; Branch = branch; Index = index; }
}

public static class NineLivesCatalog
{
    public static readonly IReadOnlyList<NineLivesNode> Nodes = System.Array.AsReadOnly(new[] {
        new NineLivesNode("A1", "Eager Pounce", "Natural Fury charge +20%. Ball speed stays the same.", "Pounce", 0),
        new NineLivesNode("A2", "Quick Claws", "Laser cooldown -25% (0.2625 seconds). Requires Laser.", "Pounce", 1),
        new NineLivesNode("A3", "Edge Hunter", "Clean returns in either outer 25% add 3% Fury. Shared 0.5-second cooldown.", "Pounce", 2),
        new NineLivesNode("A4", "Finish the Hunt", "At 3 or fewer destructible bricks, 15 active seconds without damage doubles natural Fury charge until damage.", "Pounce", 3),
        new NineLivesNode("A5", "Meteor Paw", "After Fury, Meteor Return carries into the next level. Your next genuine paddle return grants all balls at least 4 seconds of Fireball. Requires 8 total skill points earned.", "Pounce", 4),
        new NineLivesNode("B1", "Lasting Catnip", "Beneficial timed pickups and inventory effects last 25% longer (12.5 seconds).", "Play", 5),
        new NineLivesNode("B2", "Packed Lunch", "First launch each attempt grants your chosen Wide, Sticky or Laser effect for at least 6 seconds.", "Play", 6),
        new NineLivesNode("B3", "Encore", "Catch an already-active beneficial timed pickup to add 3 extra seconds.", "Play", 7),
        new NineLivesNode("B4", "Keep the Party Going", "Each new multiball clone can bounce safely once during its first 3 active seconds.", "Play", 8),
        new NineLivesNode("B5", "Catnip Party", "Catch 3 beneficial falling pickups to gain free Multiball and at least 10 seconds of Fireball on every ball, once per attempt. Requires 8 total skill points earned.", "Play", 9),
        new NineLivesNode("C1", "Helping Paw", "Base paddle width +12%.", "Survive", 10),
        new NineLivesNode("C2", "One More Life", "Start with 4 lives on your next fresh campaign start, level-select start or full retry.", "Survive", 11),
        new NineLivesNode("C3", "Shake It Off", "Timed curses last 20% less (8 seconds).", "Survive", 12),
        new NineLivesNode("C4", "Soft Landing", "After losing a life, your next launch grants at least 3 seconds of Shield Wall.", "Survive", 13),
        new NineLivesNode("C5", "Ninth Life", "Once per attempt, rescue the final ball with an upward bounce without losing a life. Requires 8 total skill points earned.", "Survive", 14)
    });
    public static NineLivesNode Find(string id) { foreach (var node in Nodes) if (node.Id == id) return node; return null; }
}
