using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityREST;

namespace UnityREST.Samples
{
    /// <summary>
    /// Typical calls against https://jsonplaceholder.typicode.com. Create an ApiConfig with that base URL
    /// and endpoints "getPost" (GET posts/{id}) and "createPost" (POST posts), then assign it here.
    /// </summary>
    public sealed class RestExamples : MonoBehaviour
    {
        [Serializable]
        public sealed class Post
        {
            public int id;
            public int userId;
            public string title;
            public string body;
        }

        [SerializeField]
        private ApiConfig api;

        [SerializeField]
        private EndpointRef getPost;

        private async UniTaskVoid Start()
        {
            // Cancelled automatically when this object is destroyed.
            var token = this.GetCancellationTokenOnDestroy();
            var client = api.Client;

            // 1. Typed GET with a path parameter and a query string. Never throws for HTTP errors.
            var post = await client.Get("posts/{id}").Path("id", 1).Query("lang", "es").SendAsync<Post>(token);

            if (post.IsSuccess)
            {
                Debug.Log($"GET: {post.Value.title}");
            }
            else
            {
                Debug.LogWarning($"GET failed: {post.Error} {post.StatusCode} {post.ErrorMessage}");
            }

            // 2. Named endpoint picked in the inspector.
            var viaEndpoint = await getPost.Request().Path("id", 2).SendAsync<Post>(token);
            Debug.Log($"Endpoint: {viaEndpoint}");

            // 3. POST a JSON body.
            var created = await client.Endpoint("createPost").Json(new Post { userId = 1, title = "Hola", body = "Desde Unity" }).SendAsync<Post>(token);
            Debug.Log($"POST: {created.StatusCode} id={created.Value?.id}");

            // 4. Raw response: zero-copy bytes and headers. Dispose to free native memory.
            using (var response = await client.Get("posts").SendAsync(token))
            {
                Debug.Log($"Raw: {response.Data.Length} bytes, content-type {response.GetHeader("Content-Type")}");
            }

            ConfigureAuth(client);
            await DownloadExamples(token);
        }

        private static void ConfigureAuth(RestClient client)
        {
            client.SetBearerToken("access-token");

            // Called once on a 401: refresh and return true to resend the request.
            client.RefreshAuthorization = async (c, ct) =>
            {
                var refreshed = await c.Post("auth/refresh").Form("refresh_token", "stored-refresh-token").SendAsync<TokenResponse>(ct);

                if (!refreshed.IsSuccess)
                {
                    return false;
                }

                c.SetBearerToken(refreshed.Value.access_token);
                return true;
            };
        }

        private static async UniTask DownloadExamples(CancellationToken token)
        {
            var cdn = new RestClient("https://picsum.photos");

            // Texture, with progress.
            var progress = new Progress<float>(p => Debug.Log($"Texture {p:P0}"));

            using (var image = await cdn.Get("256").AsTexture().Progress(progress).SendAsync(token))
            {
                if (image.IsSuccess)
                {
                    Debug.Log($"Texture {image.Texture.width}x{image.Texture.height}");
                }
            }

            // Straight to disk, without holding the file in memory.
            var path = Path.Combine(Application.temporaryCachePath, "photo.jpg");

            using (var file = await cdn.Get("1024").ToFile(path).Timeout(60f).SendAsync(token))
            {
                Debug.Log(file.IsSuccess ? $"Saved {path}" : file.ToString());
            }
        }

        [Serializable]
        private sealed class TokenResponse
        {
            public string access_token;
        }
    }
}
