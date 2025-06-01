using Unity.Collections;

namespace SceneLoader.Runtime.Core.Internal.Bridge
{
    public static class NativeArrayExtensions
    {
        public static NativeArray<T>.ReadOnly AsReadOnly<T>(this NativeArray<T> array, int count) where T : unmanaged
        {
            unsafe
            {
                return new NativeArray<T>.ReadOnly(array.m_Buffer, count, ref array.m_Safety);
            }
        }
    }
}
