using UnityEngine;

namespace UnityREST.AI
{
    /// <summary>Asset wrapper so a provider can be configured in the inspector and shared.</summary>
    [CreateAssetMenu(fileName = "ChatProvider", menuName = "UnityREST/AI Chat Provider", order = 1)]
    public sealed class ChatProviderAsset : ScriptableObject
    {
        [SerializeField]
        private ChatProviderSettings settings = ChatProviderSettings.Anthropic();

        public ChatProviderSettings Settings => settings;

        public IChatProvider CreateProvider() => settings.CreateProvider();
    }
}
