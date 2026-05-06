using System;
using System.IO;
using System.Threading.Tasks;
using ConsoleApp1;

class Program
{
    static async Task Main(string[] args)
    {
        Directory.CreateDirectory("Users");

        MudServer server = new MudServer();
        
        await server.Start(8888);

        Console.WriteLine("Server běží. Stiskni Enter pro ukončení...");
        Console.ReadLine(); 
    }
} 
