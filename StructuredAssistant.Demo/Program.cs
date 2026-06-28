Console.OutputEncoding = System.Text.Encoding.UTF8;

var settings = AppSettings.FromEnvironment();
var ollamaClient = new OllamaClient(settings);
var parser = new IntentParser(ollamaClient);
var tools = new DemoTools();
var router = new IntentRouter(tools);

var app = new AssistantApp(settings, parser, router);
await app.RunAsync();
