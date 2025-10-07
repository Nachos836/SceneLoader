using Unity.Collections;

namespace SceneLoader.Runtime.Core.Internal
{
    internal static class NativeArrayExtensions
    {
        public static NativeArray<T>.ReadOnly AsReadOnly<T>(this NativeArray<T> array, int count) where T : unmanaged
        {
            return array.GetSubArray(0, count)
                .AsReadOnly();
        }
    }
}
