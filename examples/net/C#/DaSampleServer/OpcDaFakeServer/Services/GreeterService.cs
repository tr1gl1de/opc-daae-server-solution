using ProtoBuf.Grpc;
using Shared.Contracts;

namespace OpcDaFakeServer.Services;

public class GreeterService : IGreetService
{
    public Task<Shared.Contracts.HelloReply> SayHelloAsync(Shared.Contracts.HelloRequest request, CallContext context = default)
    {
        return Task.FromResult(new Shared.Contracts.HelloReply()
        {
            Message = $"Hello {request.Name}"
        });
    }
}