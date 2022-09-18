namespace rcon.example;

using System;
using rcon.library;


public class Program
{
    public SocketClient? client;
    public RconClient? rcon;
    private int id = 0;
    private bool isConnected => client?.IsConnect ?? false;

    public static void Main(string[] args)
    {
        new Program().MainAsync(args).GetAwaiter().GetResult();
    }

    public async Task MainAsync(string[] args, CancellationToken cancellation = default)
    {
        Console.WriteLine(@"
MIT License
Built by BattleFishMan for disrespecTech.

This is a very simple rcon example.
");

        ShowHelp();

        var running = true;
        while (running)
        {
            Console.Write("\r\n>: ");
            var commands = Console.ReadLine()?.Split(" ") ?? Array.Empty<string>();
            var command = commands.Skip(1).ToArray();

            switch (commands.FirstOrDefault())
            {
                case "/connect":
                    await Connect(command, cancellation);
                    break;

                case "/list":
                    List();
                    break;

                case "/tell":
                    Tell(command);
                    break;

                case "/raw":
                    Raw(command);
                    break;

                case "/quit":
                    running = false;
                    break;

                case "/help":
                default:
                    ShowHelp();
                    break;
            }
        }
    }

    private async Task Connect(string[] args, CancellationToken cancellation)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Expected arguments missing: /connect [hostname] [port] [password]");
            return;
        }

        if (isConnected)
        {
            Console.WriteLine("Already connected");
            return;
        }

        if (!int.TryParse(args[1], out int port))
        {
            Console.WriteLine("Port not valid expected a integer");
            return;
        }

        var hostname = args[0];
        client = new SocketClient();
        if (!await client.Connect(hostname, port, cancellation))
        {
            client = null;

            Console.WriteLine($"Failed to connect to server: Ensure server is online and accessible at {hostname}:{port}");
            return;
        }

        // Setup listeners could be optimised
        client.StartListening();

        rcon = new RconClient(client);
        rcon.MessageReceived += Client_MessageReceived;

        // TODO there is a small race condition between the read thread starting and the auth being sent
        Thread.Sleep(1000);
        rcon.Auth(id++, args[2]);
    }

    private void List()
    {
        if (!CheckConnected()) return;
        rcon?.List(id++);
    }

    private void Tell(string[] args)
    {
        if (!CheckConnected()) return;
        if (args.Length < 2)
        {
            Console.WriteLine("Expected arguments missing: /tell [target] [message]");
            return;
        }

        rcon?.Tell(id++, args[0], string.Join(" ", args[1..]));
    }

    private void Raw(string[] args)
    {
        if (!CheckConnected()) return;
        if (args.Length < 1)
        {
            Console.WriteLine("Expected arguments missing: /raw [command]");
            return;
        }

        rcon?.Raw(id++, string.Join(" ", args));
    }

    private bool CheckConnected()
    {
        if (!isConnected) Console.WriteLine("You are not connected");
        return isConnected;
    }

    private void Client_MessageReceived(object? sender, Message message)
    {
        var body = string.IsNullOrWhiteSpace(message.Body) ? "[[NO MESSAGE]]" : message.Body;
        Console.WriteLine($"\r\nServer: [ID:{message.Id}] [Type:{message.Type}] Body:{body}\n");
        Console.Write("\r\n>: ");
    }

    private void ShowHelp()
    {
        Console.WriteLine(@"
Commands:

/quit - quit program

/help - shows help details

/connect [hostname] [port] [password] - trys to login to rcon server

/tell [target] [message] - message target player

/raw [command] - sends a raw rcon command to minecraft server

/list - list players on server
");
    }
}
