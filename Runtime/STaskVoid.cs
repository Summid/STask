using SFramework.Threading.Tasks.CompilerServices;
using System.Runtime.CompilerServices;

namespace SFramework.Threading.Tasks
{
    [AsyncMethodBuilder(typeof(AsyncSTaskVoidMethodBuilder))]
    public readonly struct STaskVoid
    {
        public void Forget() { }
    }
}