using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ConsoleApp1;

public class Player
{
    public string Name { get; set; }
    public string PasswordHash { get; set; }
    public string CurrentRoomId { get; set; } = "vyslechovka"; 
    public List<string> Inventory { get; set; } = new();
    public int MaxInventoryCapacity { get; set; } = 5;
    public int AttackBonus { get; set; } = 0; 
    
    
    public int HP { get; set; } = 100;
    public int MaxHP { get; set; } = 100;

    [JsonIgnore] 
    public TcpClient Client { get; set; }
    
    [JsonIgnore]
    public NetworkStream Stream => Client?.GetStream();

    public async Task SendMessage(string message) 
    {
        if (Stream != null && Client.Connected) 
        {
            try {
                byte[] data = Encoding.UTF8.GetBytes(message + "\r\n");
                await Stream.WriteAsync(data, 0, data.Length);
            } catch { }
        }
    }

    public static string HashPassword(string password) 
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}