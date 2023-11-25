using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

// Based on FileIOPlugin native function
public sealed class CodeFileFinderPlugin
{
    [SKFunction, Description("Read all text from a document")]
    public static async Task<CodeFileCollection> GetCode([Description("Path to the file to read")] string rootFolder)
    {
        var codeContent = new List<CodeFile>();

        if (!string.IsNullOrWhiteSpace(rootFolder) && Directory.Exists(rootFolder))
        {
            foreach (var codeFile in Directory.EnumerateFiles(rootFolder, "*.cs", SearchOption.AllDirectories))
            {
                codeContent.Add(new CodeFile(codeFile, await ReadAsync(codeFile)));
            }
        }

        return new CodeFileCollection(codeContent);
    }

    private static async Task<string> ReadAsync(string path)
    {
        using var reader = File.OpenText(path);

        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}

[TypeConverter(typeof(PluginReturnTypeConverter<CodeFileCollection>))]
public record CodeFileCollection(IList<CodeFile> Files)
{
}

[TypeConverter(typeof(PluginReturnTypeConverter<CodeFile>))]
public record CodeFile(string Path, string Source)
{
}

public sealed class PluginReturnTypeConverter<TDestination> : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => JsonSerializer.Deserialize<TDestination>((string)value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) => JsonSerializer.Serialize(value);
}