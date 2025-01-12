using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using LucHeart.CoreOSC;

namespace Automatic_OSC_Router;

class Program
{
    private static ConcurrentDictionary<int, OscSender> ToSendDataTo = new();

    private static void Main()
    {
        Console.Title = "Automatic OSC Router By Kanna";
        Console.WriteLine("Welcome to Automatic OSC Router by Kanna. I saw there was router software out there, but they really weren't that good, so i made this! It's fully automatic and can even automatically detect the window titles!");
        Console.WriteLine("If you appreciate my work, please consider donating: https://paypal.me/KannaVR");

        using var osc = new OscListener(new IPEndPoint(IPAddress.Loopback, 9001));

        Task.Run(async void () =>
        {
            while (true)
            {
                var message = await osc.ReceiveMessageAsync();

                if (ToSendDataTo.Count > 0)
                {
                    Console.WriteLine($"Received OSC message: {message.Address}: {string.Join(", ", message.Arguments.Select(o => o?.ToString()))}, forwarding {ToSendDataTo.Count} senders.");
                }

                foreach (var sender in ToSendDataTo)
                {
                    await sender.Value.SendAsync(message);
                }
            }
        });

        Task.Run(async () =>
        {
            while (true)
            {
                for (var i = 9002; i < 10000; i++)
                {
                    try
                    {
                        var tempListener = new OscListener(new IPEndPoint(IPAddress.Loopback, i));
                        tempListener.Dispose();

                        if (ToSendDataTo.ContainsKey(i))
                        {
                            //Console.WriteLine($"Detected OSC on port {i} shut down, removing sender");
                            ToSendDataTo.Remove(i, out _);
                        }
                    }
                    catch (Exception)
                    {
                        if (ToSendDataTo.ContainsKey(i))
                        {
                            continue;
                        }

                        // Let Process finish opening
                        Thread.Sleep(500);

                        //var process = FindProcessUsingPort(i);
                        //Console.WriteLine($"Detected OSC on port {i} from {process.MainWindowTitle}, opening sender");
                        var sender = new OscSender(new IPEndPoint(IPAddress.Loopback, i));
                        ToSendDataTo[i] = sender;
                    }
                }
                
                await Task.Delay(100);
            }
        });
        
        Console.WriteLine(""); // Spacing

        Task.Run(async () =>
        {
            var initialCursorTop = Console.CursorTop;
            Console.CursorVisible = false; // Hide the cursor

            while (true)
            {
                var currentLine = initialCursorTop;

                Console.SetCursorPosition(0, currentLine);
                Console.WriteLine("Currently routing data to:".PadRight(Console.WindowWidth));

                foreach (var entry in ToSendDataTo.ToArray())
                {
                    currentLine++;
                    Console.SetCursorPosition(0, currentLine);

                    var process = FindProcessUsingPort(entry.Key);
                    var output = !string.IsNullOrWhiteSpace(process?.MainWindowTitle)
                        ? $"Program Title: {process.MainWindowTitle}, Port: {entry.Key}"
                        : $"Port: {entry.Key}";

                    Console.WriteLine(output.PadRight(Console.WindowWidth));
                }

                var totalLines = initialCursorTop + ToSendDataTo.Count + 1;
                while (currentLine < totalLines)
                {
                    currentLine++;
                    Console.SetCursorPosition(0, currentLine);
                    Console.WriteLine(new string(' ', Console.WindowWidth));
                }

                await Task.Delay(1000);
            }
        });

        while (true)
        {
            Console.ReadLine();
        }
    }

    private static Process? FindProcessUsingPort(int portNumber)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C netstat -ano | findstr :{portNumber}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            process.WaitForExit();

            var result = process.StandardOutput.ReadToEnd();
            return Process.GetProcessById(int.Parse(result.Split(' ').Last()));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding process for port {portNumber}: {ex.Message}");
        }

        return null;
    }
}