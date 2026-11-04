namespace PCOBarbershop.Server.Settings
{
    public class OllamaSettings
    {
        public const string SectionName = "Ollama";

        // Address of the local Ollama server and the model it runs
        public string Endpoint { get; set; } = "http://localhost:11434/";
        public string Model { get; set; } = "qwen3:8b";
    }
}