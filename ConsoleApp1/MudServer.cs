using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleApp1;

public class CraftingRecipe
{
    public string Result { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public string Description { get; set; }
}

public class MudServer
{
    private TcpListener _listener;
    private List<Player> _players = new();
    private Dictionary<string, Room> _world = new();
    private List<CraftingRecipe> _recipes = new();

    public async Task Start(int port)
    {
        LoadWorld();
        LoadRecipes();

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Logger.Log($"Server spuštěn na portu {port}");

        while (true)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
    }

    private void LoadWorld()
    {
        try
        {
            string jsonString = File.ReadAllText("world.json");
            var rooms = JsonSerializer.Deserialize<List<Room>>(jsonString);

            _world.Clear();
            foreach (var r in rooms)
                _world[r.Id] = r;

            Logger.Log($"Herní svět načten: {_world.Count} místností.");
        }
        catch (Exception ex)
        {
            Logger.Log($"CHYBA při načítání světa: {ex.Message}");
        }
    }

    private void LoadRecipes()
    {
        try
        {
            string jsonString = File.ReadAllText("crafting.json");
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _recipes = JsonSerializer.Deserialize<List<CraftingRecipe>>(jsonString, options) ?? new();
            Logger.Log($"Receptury načteny: {_recipes.Count} receptů.");
            
        }
        catch (Exception ex)
        {
            Logger.Log($"CHYBA při načítání receptů: {ex.Message}");
            _recipes = new();
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        Player player = null;
        try
        {
            player = await LoginOrRegister(client);
            if (player == null) return;

            lock (_players) _players.Add(player);

            Logger.Log($"Hráč {player.Name} se připojil.");
            await player.SendMessage($"\nSystém: Přihlášen jako {player.Name}.");
            await LookAround(player);

            byte[] buffer = new byte[1024];
            while (client.Connected)
            {
                int bytesRead = await player.Stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string input = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                if (string.IsNullOrEmpty(input)) continue;

                Logger.Log($"[{player.Name}] příkaz: {input}");
                await ProcessCommand(player, input);
                await SavePlayer(player);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Chyba u hráče {player?.Name}: {ex.Message}");
        }
        finally
        {
            if (player != null)
            {
                await SavePlayer(player);
                lock (_players) _players.Remove(player);
                Logger.Log($"{player.Name} se odpojil.");
            }
            client.Close();
        }
    }

    private async Task ProcessCommand(Player player, string input)
    {
        string[] parts = input.Split(' ', 2);
        string cmd = parts[0].ToLower().Trim();
        string arg = parts.Length > 1 ? parts[1].Trim() : "";
        var room = _world[player.CurrentRoomId];

        switch (cmd)
        {
            case "prozkoumej":
                await LookAround(player);
                break;

            case "jdi":
                await MovePlayer(player, arg.ToLower());
                break;

            case "vezmi":
                lock (room)
                {
                    string item = room.Items.FirstOrDefault(i => i.ToLower() == arg.ToLower());
                    if (item != null)
                    {
                        if (player.Inventory.Count < player.MaxInventoryCapacity)
                        {
                            room.Items.Remove(item);
                            player.Inventory.Add(item);
                            player.SendMessage($"Vzal jsi: {item}").Wait();
                        }
                        else player.SendMessage("Inventář je plný.").Wait();
                    }
                    else player.SendMessage("To tu není.").Wait();
                }
                break;

            case "odloz":
                string invItem = player.Inventory.FirstOrDefault(i => i.ToLower() == arg.ToLower());
                if (invItem != null)
                {
                    player.Inventory.Remove(invItem);
                    lock (room) room.Items.Add(invItem);
                    await player.SendMessage($"Odložil jsi: {invItem}");
                }
                else await player.SendMessage("To nemáš v inventáři.");
                break;

            case "inventar":
                if (player.Inventory.Count == 0)
                    await player.SendMessage($"Inventář je prázdný. (0/{player.MaxInventoryCapacity})");
                else
                    await player.SendMessage($"Inventář [{player.Inventory.Count}/{player.MaxInventoryCapacity}]: {string.Join(", ", player.Inventory)}");
                break;

            case "mluv":
                var npc = room.NPCs.FirstOrDefault(n =>
                    n.Id.ToLower() == arg.ToLower() || n.Name.ToLower() == arg.ToLower());
                if (npc != null)
                    await player.SendMessage($"{npc.Name}: \"{npc.Dialogue}\"");
                else
                    await player.SendMessage("Nikdo takový tu není.");
                break;

            case "rekni":
                if (string.IsNullOrEmpty(arg))
                {
                    await player.SendMessage("Co chceš říct?");
                }
                else
                {
                    string msg = $"[{room.Name}] {player.Name} říká: {arg}";
                    Logger.Log(msg);
                    var playersInRoom = new List<Player>();
                    lock (_players)
                        playersInRoom = _players.Where(p => p.CurrentRoomId == player.CurrentRoomId).ToList();
                    foreach (var p in playersInRoom)
                        await p.SendMessage(msg);
                }
                break;

            case "krik":
                if (string.IsNullOrEmpty(arg))
                {
                    await player.SendMessage("Co chceš vykřiknout?");
                }
                else
                {
                    string shout = $"[KŘIK] {player.Name}: {arg}";
                    Logger.Log(shout);
                    List<Player> allPlayers;
                    lock (_players) allPlayers = _players.ToList();
                    foreach (var p in allPlayers)
                        await p.SendMessage(shout);
                }
                break;

            case "utoc":
                await HandleCombat(player, room, arg);
                break;

            case "vyrob":
                await HandleCrafting(player, arg);
                break;

            case "pomoc":
                await player.SendMessage(
                    "\n=== NÁPOVĚDA ===\n" +
                    "prozkoumej          - Prohlédneš si místnost\n" +
                    "jdi <směr>          - Přesun do jiné místnosti (např. jdi sever)\n" +
                    "vezmi <předmět>     - Vezmeš předmět ze země\n" +
                    "odloz <předmět>      - Odložíš předmět z inventáře na zem\n" +
                    "inventar            - Zobrazí obsah tvého inventáře\n" +
                    "mluv <jméno>        - Promluvíš s NPC (např. mluv Josef)\n" +
                    "rekni <zpráva>      - Řekneš něco hráčům ve stejné místnosti\n" +
                    "krik <zpráva>       - Vykřičíš zprávu všem hráčům na serveru\n" +
                    "utoc <jméno>        - Napadneš NPC (např. utoc robot)\n" +
                    "hod <předmět>       - Hodíš předmět na nepřítele (např. hod molotov)\n"+
                    "vyrob <předmět>     - Vyrobíš předmět ze surovin (např. vyrob paklic)\n" +
                    "pomoc               - Zobrazí tuto nápovědu\n" +
                    "================\n"
                );
                break;
            
            case "pouzij":
                string itemToUse = player.Inventory.FirstOrDefault(i => i.ToLower() == arg.ToLower());
                if (itemToUse == null)
                {
                    await player.SendMessage("Takový předmět v inventáři nemáš.");
                    break;
                }

                if (itemToUse.ToLower() == "energetak")
                {
                    player.Inventory.Remove(itemToUse);
                    player.HP = Math.Min(player.MaxHP, player.HP + 40); 
                    await player.SendMessage($"✓ Vypil jsi energetak. Vyléčil jsi se o 40 HP. Aktuální zdraví: {player.HP}/{player.MaxHP} HP.");
                }
                else
                {
                    await player.SendMessage($"Předmět '{itemToUse}' nelze použít.");
                }
                break;
            
            case "hod":
                if (string.IsNullOrEmpty(arg))
                {
                    await player.SendMessage("Co chceš hodit? (např. hod molotov)");
                }
                else if (arg.ToLower() == "molotov")
                {
                    string molotov = player.Inventory.FirstOrDefault(i => i.ToLower() == "molotov");
                    if (molotov == null)
                    {
                        await player.SendMessage("Žádný molotov v inventáři nemáš.");
                    }
                    else
                    {
                        
                        await HandleMolotovThrow(player, room);
                    }
                }
                else
                {
                    await player.SendMessage("Tenhle předmět nemůžeš hodit.");
                }
                break;

            default:
                await player.SendMessage("Nerozumím. Zkus 'pomoc'.");
                break;
        }
    }

    private async Task HandleCombat(Player player, Room room, string arg)
    {
        NPC target;
        lock (room)
            target = room.NPCs.FirstOrDefault(n =>
                n.Id.ToLower() == arg.ToLower() || n.Name.ToLower() == arg.ToLower());

        if (target == null)
        {
            await player.SendMessage("Nemáš na koho útočit.");
            return;
        }

        
        int dmg = 10;
        if (player.Inventory.Any(i => i.ToLower() == "nuz"))
        {
            dmg += 10; 
        }
        dmg += player.AttackBonus;

        lock (target) target.HP -= dmg;
        await player.SendMessage($"⚔ Zasáhl jsi {target.Name} za {dmg} HP. Zbývá mu {target.HP} HP.");

        if (target.HP <= 0)
        {
            await player.SendMessage($"✓ {target.Name} padl k zemi!");
            if (!string.IsNullOrEmpty(target.Reward))
            {
                lock (room) room.Items.Add(target.Reward);
                await player.SendMessage($"🎁 Upadl z něj: {target.Reward}");
            }
            lock (room) room.NPCs.Remove(target);
            return; 
        }

        
        player.HP -= target.Attack;
        await player.SendMessage($"💥 {target.Name} ti vrací ránu za {target.Attack} HP! Tvůj stav: {player.HP}/{player.MaxHP} HP.");

        
        if (player.HP <= 0)
        {
            await player.SendMessage("\n💀 ZEMŘEL JSI! Sekuriťáci tě zmlátili do bezvědomí a odtáhli zpět.");
        
            
            player.CurrentRoomId = "vyslechovka";
            player.HP = player.MaxHP;
            player.Inventory.Clear();
            player.AttackBonus = 0; 

            await LookAround(player);
        }
    }
    
    private async Task HandleMolotovThrow(Player player, Room room)
    {
        NPC target;
        lock (room)
            target = room.NPCs.FirstOrDefault(); // Vybere první NPC v místnosti

        if (target == null)
        {
            await player.SendMessage("Není tu nikdo, na koho bys mohl molotov hodit.");
            return;
        }

        
        string itemInInventory = player.Inventory.First(i => i.ToLower() == "molotov");
        player.Inventory.Remove(itemInInventory);

        int molotovDmg = 30;
        lock (target) target.HP -= molotovDmg;

        await player.SendMessage($"🔥 Hodil jsi molotov! Místnost zachvátily plameny.");
        await player.SendMessage($"💥 {target.Name} dostal přímý zásah za {molotovDmg} HP! Zbývá mu {target.HP} HP.");

        
        if (target.HP <= 0)
        {
            await player.SendMessage($"✓ {target.Name} uhořel v plamenech!");
            if (!string.IsNullOrEmpty(target.Reward))
            {
                lock (room) room.Items.Add(target.Reward);
                await player.SendMessage($"🎁 Upadl z něj: {target.Reward}");
            }
            lock (room) room.NPCs.Remove(target);
        }
        else
        {
            await player.SendMessage($"⚠️ {target.Name} je naštvaný a hoří! Teď je na řadě tvůj útok (příkaz 'utoc').");
        }
    }

    private async Task HandleCrafting(Player player, string arg)
    {
        
        var recipe = _recipes.FirstOrDefault(r => string.Equals(r.Result, arg, StringComparison.OrdinalIgnoreCase));

        if (recipe == null)
        {
            await player.SendMessage($"Nevíš jak vyrobit '{arg}'. Zkus jiný předmět.");
            return;
        }

        
        foreach (var ingredient in recipe.Ingredients)
        {
            bool obsazeno = player.Inventory.Any(i => string.Equals(i, ingredient, StringComparison.OrdinalIgnoreCase));
            if (!obsazeno)
            {
                await player.SendMessage($"Chybí ti: {ingredient}. Potřebuješ: {string.Join(", ", recipe.Ingredients)}");
                return;
            }
        }

        
        foreach (var ingredient in recipe.Ingredients)
        {
            string itemInInventory = player.Inventory.First(i => string.Equals(i, ingredient, StringComparison.OrdinalIgnoreCase));
            player.Inventory.Remove(itemInInventory);
        }

        
        player.Inventory.Add(recipe.Result.ToLower()); 
        await player.SendMessage($"✓ Vyrobil jsi: {recipe.Result}! ({recipe.Description})");
    }

    private async Task LookAround(Player player)
    {
        var room = _world[player.CurrentRoomId];
        var sb = new StringBuilder();

        sb.AppendLine($"❤️ Životy: {player.HP}/{player.MaxHP}");
        sb.AppendLine($"\n=== {room.Name} ===");
        sb.AppendLine(room.Description);

        if (room.Items.Count > 0)
            sb.AppendLine("📦 Předměty: " + string.Join(", ", room.Items));
        else
            sb.AppendLine("📦 Předměty: žádné");

        sb.AppendLine("🚪 Východy: " + string.Join(", ", room.Exits.Keys));

        if (room.NPCs.Count > 0)
            sb.AppendLine("👤 Postavy: " + string.Join(", ", room.NPCs.Select(n => $"{n.Name} (HP:{n.HP})")));

        List<Player> others;
        lock (_players)
            others = _players.Where(p => p.CurrentRoomId == player.CurrentRoomId && p != player).ToList();

        if (others.Any())
            sb.AppendLine("🧑 Hráči: " + string.Join(", ", others.Select(o => o.Name)));

        await player.SendMessage(sb.ToString());
    }

    private async Task MovePlayer(Player player, string direction)
    {
        var currentRoom = _world[player.CurrentRoomId];

        if (!currentRoom.Exits.TryGetValue(direction, out string nextRoomId))
        {
            await player.SendMessage("Tudy cesta nevede.");
            return;
        }

        
        if (currentRoom.LockedExits != null && currentRoom.LockedExits.TryGetValue(direction, out string requiredItem))
        {
            if (!player.Inventory.Contains(requiredItem))
            {
                await player.SendMessage($"🔒 Dveře jsou zamčené. Potřebuješ: {requiredItem}");
                return;
            }
            await player.SendMessage($"🔓 Použil jsi {requiredItem} a odemkl dveře.");
        }

        
        List<Player> inCurrentRoom;
        lock (_players)
            inCurrentRoom = _players.Where(p => p.CurrentRoomId == player.CurrentRoomId && p != player).ToList();
        foreach (var p in inCurrentRoom)
            await p.SendMessage($"[ {player.Name} odešel směrem: {direction} ]");

        player.CurrentRoomId = nextRoomId;

        
        List<Player> inNextRoom;
        lock (_players)
            inNextRoom = _players.Where(p => p.CurrentRoomId == nextRoomId && p != player).ToList();
        foreach (var p in inNextRoom)
            await p.SendMessage($"[ {player.Name} přišel do místnosti ]");

        await LookAround(player);

        
        if (nextRoomId == "vitezstvi")
        {
            await player.SendMessage("\n************************************");
            await player.SendMessage("🏆 GRATULUJI! Unikl jsi s daty z Arasaky!");
            await player.SendMessage("HRA DOKONČENA.");
            await player.SendMessage("************************************\n");
            Logger.Log($"Hráč {player.Name} dokončil hru!");
            await SaveGameCompletion(player);
        }
    }

    private async Task SaveGameCompletion(Player player)
    {
        string statsFile = "stats.json";
        List<Dictionary<string, string>> stats = new();

        if (File.Exists(statsFile))
        {
            try
            {
                string existing = await File.ReadAllTextAsync(statsFile);
                stats = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(existing) ?? new();
            }
            catch { stats = new(); }
        }

        stats.Add(new Dictionary<string, string>
        {
            { "player", player.Name },
            { "time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
        });

        string json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(statsFile, json);

        
        var sb = new StringBuilder();
        sb.AppendLine("\n=== ŽEBŘÍČEK VÍTĚZŮ ===");
        for (int i = 0; i < stats.Count; i++)
            sb.AppendLine($"{i + 1}. {stats[i]["player"]} - {stats[i]["time"]}");
        sb.AppendLine("======================");
        await player.SendMessage(sb.ToString());
    }

    private async Task<Player> LoginOrRegister(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        await SendToClient(stream, "Vítejte v mrakodrapu Arasaka.\nZadejte jméno: ");
        string name = await ReadFromClient(stream);

        if (string.IsNullOrWhiteSpace(name)) return null;

        string filePath = $"Users/{name}.json";
        Player player;

        if (File.Exists(filePath))
        {
            await SendToClient(stream, "Zadejte heslo: ");
            string pass = await ReadFromClient(stream);
            string hash = Player.HashPassword(pass);

            string json = await File.ReadAllTextAsync(filePath);
            player = JsonSerializer.Deserialize<Player>(json);

            if (player.PasswordHash != hash)
            {
                await SendToClient(stream, "Špatné heslo. Odpojování...\n");
                Logger.Log($"Neúspěšné přihlášení pro: {name}");
                return null;
            }
            Logger.Log($"Hráč {name} se přihlásil.");
        }
        else
        {
            await SendToClient(stream, "Nové jméno. Zvolte si heslo: ");
            string pass = await ReadFromClient(stream);

            player = new Player
            {
                Name = name,
                PasswordHash = Player.HashPassword(pass),
                CurrentRoomId = "vyslechovka"
            };
            await SavePlayer(player);
            Logger.Log($"Vytvořen nový účet: {name}");
        }

        player.Client = client;
        return player;
    }

    private async Task SavePlayer(Player player)
    {
        Directory.CreateDirectory("Users");
        string json = JsonSerializer.Serialize(player, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync($"Users/{player.Name}.json", json);
    }

    private async Task SendToClient(NetworkStream stream, string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(data, 0, data.Length);
    }

    private async Task<string> ReadFromClient(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        int read = await stream.ReadAsync(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, read).Trim();
    }
}