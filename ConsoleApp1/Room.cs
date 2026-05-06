using System.Collections.Generic;

namespace ConsoleApp1;

public class Room
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Dictionary<string, string> Exits { get; set; } = new();
    public List<string> Items { get; set; } = new(); 
    public Dictionary<string, string> LockedExits { get; set; } = new();
    public List<NPC> NPCs { get; set; } = new();
}
