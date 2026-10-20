using System;
using System.Collections;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace UnityREST.Tests
{
    using AI;
    using AI.Mcp;

    public sealed class AiIntegrationTests
    {
        private TestServer _server;

        [SetUp]
        public void SetUp() => _server = new TestServer();

        [TearDown]
        public void TearDown() => _server.Dispose();

        private ChatSession CreateSession(ChatProviderSettings settings)
        {
            settings.baseUrl = _server.BaseUrl + "/v1";
            settings.apiKey = "test-key";
            var session = new ChatSession(settings.CreateProvider(), "Be brief.");
            session.Tools.Add(Tool.Create("get_weather", "Weather for a city", new ToolSchema().String("city", "City name"),
                args => $"{{\"city\":\"{(string)args["city"]}\",\"temp\":21}}"));
            return session;
        }

        [UnityTest]
        public IEnumerator OpenAIRunsToolLoop() => UniTask.ToCoroutine(async () =>
        {
            var calls = new TestServer.Counter();
            _server.Map("/v1/chat/completions", (context, _) => TestServer.Respond(context, 200, calls.Next() == 1
                ? "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"c1\",\"type\":\"function\",\"function\":{\"name\":\"get_weather\",\"arguments\":\"{\\\"city\\\":\\\"Lima\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}"
                : "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"21 degrees in Lima.\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":20,\"completion_tokens\":6}}"));

            var session = CreateSession(ChatProviderSettings.OpenAI());
            var response = await session.SendAsync("Weather in Lima?");

            Assert.IsTrue(response.IsSuccess, response.ToString());
            Assert.AreEqual("21 degrees in Lima.", response.Text);
            Assert.AreEqual(30, session.TotalUsage.InputTokens);

            var second = JObject.Parse(_server.Snapshot()[1].Body);
            var messages = (JArray)second["messages"];
            Assert.AreEqual("system", (string)messages[0]["role"]);
            Assert.AreEqual("c1", (string)messages[2]["tool_calls"][0]["id"]);
            Assert.AreEqual("tool", (string)messages[3]["role"]);
            StringAssert.Contains("\"temp\":21", (string)messages[3]["content"]);
            Assert.AreEqual(16000, (int)second["max_completion_tokens"]);
            Assert.AreEqual("Bearer test-key", _server.Snapshot()[0].Headers["Authorization"]);
        });

        [UnityTest]
        public IEnumerator OpenAIStreamsTextAndToolCalls() => UniTask.ToCoroutine(async () =>
        {
            var calls = new TestServer.Counter();
            _server.Map("/v1/chat/completions", (context, _) => TestServer.Stream(context, calls.Next() == 1
                ? new[]
                {
                    "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"c9\",\"function\":{\"name\":\"get_weather\",\"arguments\":\"{\\\"ci\"}}]}}]}\n\n",
                    "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"ty\\\":\\\"Quito\\\"}\"}}]},\"finish_reason\":\"tool_calls\"}]}\n\n",
                    "data: [DONE]\n\n"
                }
                : new[]
                {
                    "data: {\"choices\":[{\"delta\":{\"content\":\"Sunny \"}}]}\n\n",
                    "data: {\"choices\":[{\"delta\":{\"content\":\"in Quito.\"},\"finish_reason\":\"stop\"}]}\n\n",
                    "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3}}\n\n",
                    "data: [DONE]\n\n"
                }));

            var session = CreateSession(ChatProviderSettings.OpenAI());
            var streamed = new StringBuilder();
            var response = await session.SendAsync("Weather?", delta => streamed.Append(delta));

            Assert.IsTrue(response.IsSuccess, response.ToString());
            Assert.AreEqual("Sunny in Quito.", streamed.ToString());
            Assert.AreEqual("Sunny in Quito.", response.Text);
            Assert.AreEqual(3, response.Usage.OutputTokens);

            var toolMessage = ((JArray)JObject.Parse(_server.Snapshot()[1].Body)["messages"]).Last;
            StringAssert.Contains("Quito", (string)toolMessage["content"]);
        });

        [UnityTest]
        public IEnumerator AnthropicStreamReplaysThinkingBlocks() => UniTask.ToCoroutine(async () =>
        {
            var calls = new TestServer.Counter();
            _server.Map("/v1/messages", (context, _) => TestServer.Stream(context, calls.Next() == 1
                ? new[]
                {
                    "event: message_start\ndata: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":12}}}\n\n",
                    "event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"thinking\",\"thinking\":\"\"}}\n\n",
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"thinking_delta\",\"thinking\":\"Need weather.\"}}\n\n",
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"signature_delta\",\"signature\":\"sig-123\"}}\n\n",
                    "event: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\n",
                    "event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":1,\"content_block\":{\"type\":\"tool_use\",\"id\":\"tu_1\",\"name\":\"get_weather\",\"input\":{}}}\n\n",
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"{\\\"city\\\": \"}}\n\n",
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":1,\"delta\":{\"type\":\"input_json_delta\",\"partial_json\":\"\\\"Oslo\\\"}\"}}\n\n",
                    "event: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":1}\n\n",
                    "event: message_delta\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"tool_use\"},\"usage\":{\"output_tokens\":30}}\n\n",
                    "event: message_stop\ndata: {\"type\":\"message_stop\"}\n\n"
                }
                : new[]
                {
                    "event: content_block_start\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n",
                    "event: content_block_delta\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Oslo is 21.\"}}\n\n",
                    "event: content_block_stop\ndata: {\"type\":\"content_block_stop\",\"index\":0}\n\n",
                    "event: message_delta\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":5}}\n\n"
                }));

            var session = CreateSession(ChatProviderSettings.Anthropic());
            var response = await session.SendAsync("Weather in Oslo?", _ => { });

            Assert.IsTrue(response.IsSuccess, response.ToString());
            Assert.AreEqual("Oslo is 21.", response.Text);

            var first = _server.Snapshot()[0];
            Assert.AreEqual("test-key", first.Headers["x-api-key"]);
            Assert.AreEqual("2023-06-01", first.Headers["anthropic-version"]);
            var firstBody = JObject.Parse(first.Body);
            Assert.AreEqual("Be brief.", (string)firstBody["system"]);
            Assert.AreEqual("claude-opus-5-5", (string)firstBody["model"]);
            Assert.IsNull(firstBody["temperature"]);

            var messages = (JArray)JObject.Parse(_server.Snapshot()[1].Body)["messages"];
            var assistant = (JArray)messages[1]["content"];
            Assert.AreEqual("thinking", (string)assistant[0]["type"]);
            Assert.AreEqual("Need weather.", (string)assistant[0]["thinking"]);
            Assert.AreEqual("sig-123", (string)assistant[0]["signature"]);
            Assert.AreEqual("Oslo", (string)assistant[1]["input"]["city"]);
            Assert.AreEqual("tool_result", (string)messages[2]["content"][0]["type"]);
            Assert.AreEqual("tu_1", (string)messages[2]["content"][0]["tool_use_id"]);
        });

        [UnityTest]
        public IEnumerator GeminiRunsToolLoop() => UniTask.ToCoroutine(async () =>
        {
            var calls = new TestServer.Counter();
            _server.Map("/v1/models/gemini-2.5-flash:generateContent", (context, _) => TestServer.Respond(context, 200, calls.Next() == 1
                ? "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"functionCall\":{\"name\":\"get_weather\",\"args\":{\"city\":\"Rome\"}},\"thoughtSignature\":\"ts1\"}]},\"finishReason\":\"STOP\"}]}"
                : "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"Rome: 21.\"}]},\"finishReason\":\"STOP\"}],\"usageMetadata\":{\"promptTokenCount\":9,\"candidatesTokenCount\":4}}"));

            var session = CreateSession(ChatProviderSettings.Gemini());
            var response = await session.SendAsync("Weather in Rome?");

            Assert.IsTrue(response.IsSuccess, response.ToString());
            Assert.AreEqual("Rome: 21.", response.Text);

            var second = JObject.Parse(_server.Snapshot()[1].Body);
            var contents = (JArray)second["contents"];
            Assert.AreEqual("ts1", (string)contents[1]["parts"][0]["thoughtSignature"]);
            Assert.AreEqual("get_weather", (string)contents[2]["parts"][0]["functionResponse"]["name"]);
            Assert.AreEqual("Be brief.", (string)second["systemInstruction"]["parts"][0]["text"]);
            Assert.AreEqual("test-key", _server.Snapshot()[0].Headers["x-goog-api-key"]);
        });

        [UnityTest]
        public IEnumerator ReportsProviderErrors() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/v1/messages", 400, "{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":\"max_tokens: too large\"}}");

            var session = CreateSession(ChatProviderSettings.Anthropic());
            var response = await session.SendAsync("Hi");

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(400, response.StatusCode);
            Assert.AreEqual("max_tokens: too large", response.ErrorMessage);
            Assert.AreEqual(1, session.History.Count, "A failed turn is rolled back.");
        });

        [UnityTest]
        public IEnumerator McpClientListsAndCallsTools() => UniTask.ToCoroutine(async () =>
        {
            _server.Map("/mcp", async (context, received) =>
            {
                var request = JObject.Parse(received.Body);
                var id = request["id"];

                switch ((string)request["method"])
                {
                    case "initialize":
                        context.Response.AddHeader("Mcp-Session-Id", "session-42");
                        await TestServer.Respond(context, 200,
                            $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{{\"tools\":{{}}}},\"serverInfo\":{{\"name\":\"test-mcp\",\"version\":\"1\"}}}}}}");
                        break;
                    case "notifications/initialized":
                        await TestServer.Respond(context, 202, string.Empty);
                        break;
                    case "tools/list":
                        await TestServer.Respond(context, 200,
                            $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"tools\":[{{\"name\":\"dice.roll\",\"description\":\"Roll dice\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{\"sides\":{{\"type\":\"integer\"}}}}}}}}]}}}}");
                        break;
                    case "tools/call":
                        // Answer over SSE, with a notification first, as Streamable HTTP servers may do.
                        await TestServer.Stream(context, new[]
                        {
                            "event: message\ndata: {\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}\n\n",
                            $"event: message\ndata: {{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{{\"content\":[{{\"type\":\"text\",\"text\":\"rolled {(int)request["params"]["arguments"]["sides"]}\"}}]}}}}\n\n"
                        });
                        break;
                }
            });

            var mcp = new McpClient(_server.BaseUrl + "/mcp", "mcp-token");
            var tools = await mcp.GetToolsAsync("remote_");

            Assert.AreEqual("test-mcp", (string)mcp.ServerInfo["name"]);
            Assert.AreEqual("remote_dice_roll", tools.Single().Name);
            Assert.AreEqual("rolled 20", await tools[0].InvokeAsync(new JObject { ["sides"] = 20 }, default));

            var requests = _server.Snapshot();
            Assert.AreEqual("Bearer mcp-token", requests[0].Headers["Authorization"]);
            Assert.IsNull(requests[0].Headers["Mcp-Session-Id"]);
            Assert.AreEqual("session-42", requests[1].Headers["Mcp-Session-Id"]);
            Assert.AreEqual("2025-06-18", requests.Last().Headers["MCP-Protocol-Version"]);
            StringAssert.Contains("text/event-stream", requests[0].Headers["Accept"]);
        });
    }
}
