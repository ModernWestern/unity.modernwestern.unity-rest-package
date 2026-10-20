using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityREST.AI;
using UnityREST.AI.Mcp;

namespace UnityREST.Samples
{
    /// <summary>
    /// An assistant that answers with any provider, streams its reply, uses a local tool that reads the scene
    /// and, optionally, every tool of a remote MCP server.
    /// </summary>
    public sealed class AiChatExample : MonoBehaviour
    {
        [SerializeField, Tooltip("Assets > Create > UnityREST > AI Chat Provider")]
        private ChatProviderAsset provider;

        [SerializeField, Tooltip("Optional Streamable HTTP MCP endpoint, e.g. https://example.com/mcp")]
        private string mcpUrl;

        [SerializeField, TextArea]
        private string question = "How many objects are in the scene, and what is the first one called?";

        private async UniTaskVoid Start()
        {
            var token = this.GetCancellationTokenOnDestroy();
            var chat = new ChatSession(provider.CreateProvider(), "You are a concise assistant that knows this Unity scene.");

            // Local tool: the model decides when to call it.
            chat.Tools.Add(Tool.Create("scene_objects", "Lists root objects of the active scene.",
                new ToolSchema().Integer("limit", "Maximum names to return", false),
                args =>
                {
                    var roots = gameObject.scene.GetRootGameObjects();
                    var limit = args.Value<int?>("limit") ?? roots.Length;
                    var names = new JArray();

                    for (var i = 0; i < roots.Length && i < limit; i++)
                    {
                        names.Add(roots[i].name);
                    }

                    return new JObject { ["count"] = roots.Length, ["names"] = names }.ToString();
                }));

            // Remote tools from an MCP server, usable with any provider.
            if (!string.IsNullOrEmpty(mcpUrl))
            {
                var mcp = new McpClient(mcpUrl);
                chat.Tools.AddRange(await mcp.GetToolsAsync("mcp_", token));
            }

            // Ask before running tools (here: always allow, but log).
            chat.ApproveToolCall = (call, _) =>
            {
                Debug.Log($"Model wants to run {call}");
                return UniTask.FromResult(true);
            };

            var reply = await chat.SendAsync(question, delta => Debug.Log(delta), token);

            Debug.Log(reply.IsSuccess
                ? $"Answer ({reply.FinishReason}, {chat.TotalUsage}): {reply.Text}"
                : $"Chat failed: {reply.Error} {reply.StatusCode} {reply.ErrorMessage}");

            // Images work too with vision models.
            await DescribeScreenshot(chat, token);
        }

        private async UniTask DescribeScreenshot(ChatSession chat, CancellationToken token)
        {
            await UniTask.WaitForEndOfFrame(this, token);
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            var reply = await chat.SendAsync(ChatMessage.User("Describe this frame in one sentence.", ChatImage.FromTexture(screenshot)), null, token);
            Destroy(screenshot);
            Debug.Log(reply.Text);
        }
    }
}
