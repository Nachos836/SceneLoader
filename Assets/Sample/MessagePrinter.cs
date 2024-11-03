using UnityEngine;

namespace SceneLoader.Sample
{
    internal sealed class MessagePrinter : MonoBehaviour
    {
        public void Print(string message) => Debug.Log(message);
    }
}
