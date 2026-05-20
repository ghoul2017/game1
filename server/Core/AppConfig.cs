using Shared.Core.Log;

namespace Shared.Core;

public class AppConfig
{
    public required ServerOptions Server { get; init; }
    public required MongoDbOptions MongoDb { get; init; }
    public required TablesOptions Tables { get; init; }
    public required LogOptions Log { get; init; }
}
