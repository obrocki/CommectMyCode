/// 

using Microsoft.Extensions.Configuration;

internal sealed class Env
{

    internal static string? Get(string name)
    {
        name = $"AzureOpenAI:{name}";

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Env>()
            .Build();

        var value = configuration[name];
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = Environment.GetEnvironmentVariable(name);

        Console.WriteLine(value);
        return value;
    }
}