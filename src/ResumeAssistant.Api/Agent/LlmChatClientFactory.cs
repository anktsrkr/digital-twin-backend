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
            return new LocalModelChatClient(chatClient.AsIChatClient());
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
        return new LocalModelChatClient(defaultClient.GetChatClient(llmOptions.Local.Model ?? "local-model").AsIChatClient());
    }
}

/// <summary>
/// Adapts local OpenAI-compatible endpoints (LM Studio, Ollama, vLLM, LocalAI) by using non-streaming completions
/// under the hood and streaming them progressively. This prevents streaming tool-call index parsing incompatibilities
/// with local model runtimes while preserving high-throughput streaming for the frontend AG-UI client.
/// Suppresses reasoning/thinking tokens so that the conversation trace matches clean standard AG-UI client/server behavior.
/// </summary>
public sealed class LocalModelChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        foreach (var msg in response.Messages)
        {
            var filteredContents = new List<AIContent>();
            foreach (var content in msg.Contents)
            {
                // Suppress reasoning / thinking content from being emitted as reasoning events
                if (content is TextReasoningContent)
                {
                    continue;
                }

                if (content is TextContent tc)
                {
                    var text = tc.Text;
                    if (!string.IsNullOrEmpty(text) && text.Contains("<think>"))
                    {
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"<think>[\s\S]*?</think>", string.Empty).TrimStart();
                    }
                    if (!string.IsNullOrEmpty(text))
                    {
                        filteredContents.Add(new TextContent(text));
                    }
                }
                else
                {
                    filteredContents.Add(content);
                }
            }

            if (filteredContents.Count > 0)
            {
                yield return new ChatResponseUpdate
                {
                    Role = msg.Role,
                    AuthorName = msg.AuthorName,
                    ResponseId = response.ResponseId,
                    Contents = filteredContents
                };
            }
        }
    }
}
