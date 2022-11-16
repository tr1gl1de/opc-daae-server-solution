// See https://aka.ms/new-console-template for more information

using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;
using Shared.Contracts;

namespace Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var httpHandler = new HttpClientHandler();
        httpHandler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        var channel = GrpcChannel.ForAddress("https://localhost:7031",
            new GrpcChannelOptions { HttpHandler = httpHandler });
        var client = channel.CreateGrpcService<IGreetService>();

        var reply = await client.SayHelloAsync(
            new HelloRequest()
            {
                Name = "GreetClient"
            });
        
        Console.WriteLine($"Greeting: {reply.Message}");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}