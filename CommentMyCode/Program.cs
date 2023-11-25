using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Text;
using System.Text;

Console.WriteLine("Specify directory with code to comment:");

string directory = Environment.CurrentDirectory;

while (true)
{
    string input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;
    if (Directory.Exists(input))
    {
        directory = input;
        break;
    }
    Console.WriteLine("Directory does not exist. Please specify a valid directory:");
}

// Initialize the kernel
IKernel kernel = Kernel.Builder
    .WithLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
    .WithAzureOpenAIChatCompletionService(Env.Get("ModelType"), Env.Get("Endpoint"), Env.Get("ApiKey"))
    .Build();

// Download a document and create embeddings for it
ISemanticTextMemory memory = new MemoryBuilder()
    .WithLoggerFactory(kernel.LoggerFactory)
    .WithMemoryStore(new VolatileMemoryStore())
    .WithAzureOpenAITextEmbeddingGenerationService(Env.Get("EmbeddingModel"), Env.Get("Endpoint"), Env.Get("ApiKey"))
    .Build();


// Import the function that will find all code files in the directory
kernel.ImportFunctions(new CodeFileFinderPlugin(), nameof(CodeFileFinderPlugin));
var function = kernel.Functions.GetFunction(nameof(CodeFileFinderPlugin), nameof(CodeFileFinderPlugin.GetCode));
var codeFiles = await kernel.RunAsync(directory, function);
var fileToComment = codeFiles.GetValue<CodeFileCollection>();

// Create embeddings for each paragraph in the code files
foreach (var (file, paragraphs) in from file in fileToComment.Files
                                   let paragraphs = TextChunker.SplitPlainTextParagraphs(TextChunker.SplitPlainTextLines(file.Source, 512), 5120)
                                   select (file, paragraphs))
{
    for (int i = 0; i < paragraphs.Count; i++)
        await memory.SaveInformationAsync(file.Path, paragraphs[i], $"paragraph{i}");
}

// Create a new chat
IChatCompletion ai = kernel.GetService<IChatCompletion>();
ChatHistory chat = ai.CreateNewChat("You are an expert in explaining programming languages. You area great at explaining what given code does and can provide valuable comments to the code.");

StringBuilder builder = new();

foreach (var file in fileToComment.Files)
{
    Console.WriteLine();
    Console.WriteLine($"File {file.Path}. Skip [s] or comment and explain [c]?");

    char key = Console.ReadKey().KeyChar;

    if (key == 's') continue;

    string question = $"Rewrite the code in {file.Path} to include comments that explaining what the code does. Code only.";

    await foreach (var result in memory.SearchAsync(file.Path, question, limit: 3))
        builder.AppendLine(result.Metadata.Text);

    int contextToRemove = -1;
    if (builder.Length != 0)
    {
        builder.Insert(0, "Here's some additional information: ");
        contextToRemove = chat.Count;
        chat.AddUserMessage(builder.ToString());
    }

    chat.AddUserMessage(question);

    builder.Clear();
    await foreach (string message in ai.GenerateMessageStreamAsync(chat))
    {
        Console.Write(message);
        builder.Append(message);
    }

    Console.WriteLine();

    chat.AddAssistantMessage(builder.ToString());

    if (contextToRemove >= 0) chat.RemoveAt(contextToRemove);
    Console.WriteLine();
}