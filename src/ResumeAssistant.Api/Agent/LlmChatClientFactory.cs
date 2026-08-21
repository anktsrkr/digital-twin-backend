using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using ResumeAssistant.Api.Configuration;
using ChatClient = OpenAI.Chat.ChatClient;

namespace ResumeAssistant.Api.Agent;

public static class LlmChatClientFactory
{
    public static IChatClient CreateChatClient(
        LlmOptions llmOptions,
        ILogger logger)
    {
        // 1. Local LLM Mode (LM Studio / Ollama)
        if (llmOptions.IsLocal && !string.IsNullOrWhiteSpace(llmOptions.Local.Endpoint))
        {
            logger.LogInformation(
                "Configuring Local LLM (LM Studio) IChatClient at endpoint '{Endpoint}' with model '{Model}'",
                llmOptions.Local.Endpoint, llmOptions.Local.Model);

            var localClientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(llmOptions.Local.Endpoint)
            };

            var localOpenAiClient = new OpenAIClient(
                new ApiKeyCredential(string.IsNullOrWhiteSpace(llmOptions.Local.ApiKey) ? "lm-studio" : llmOptions.Local.ApiKey),
                localClientOptions);

            ChatClient chatClient = localOpenAiClient.GetChatClient(llmOptions.Local.Model);
            return chatClient.AsIChatClient();
        }

        // 2. Cloud Mode (Cloudflare Workers AI)
        if (llmOptions.IsCloud && llmOptions.Cloud.IsConfigured)
        {
            string endpoint = llmOptions.Cloud.GetResolvedBaseUrl();
            logger.LogInformation(
                "Configuring Cloudflare Workers AI IChatClient at endpoint '{Endpoint}' with model '{Model}'",
                endpoint, llmOptions.Cloud.Model);

            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            };

            var openAIClient = new OpenAIClient(new ApiKeyCredential(llmOptions.Cloud.ApiToken!), clientOptions);
            ChatClient chatClient = openAIClient.GetChatClient(llmOptions.Cloud.Model);
            return chatClient.AsIChatClient();
        }

        // 3. Fallback to LM Studio local server at http://localhost:1234/v1
        logger.LogInformation("Fallback: Connecting to LM Studio local server at http://localhost:1234/v1");
        var defaultLocalOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("http://localhost:1234/v1")
        };
        var defaultClient = new OpenAIClient(new ApiKeyCredential("lm-studio"), defaultLocalOptions);
        return defaultClient.GetChatClient(llmOptions.Local.Model ?? "local-model").AsIChatClient();
    }
}
