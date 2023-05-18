namespace SFramework.Threading.Tasks
{
    /// <summary>
    /// PlayerLoopSystem的迭代对象
    /// 提供<see cref="MoveNext"/>方法供PlayerLoopSystem迭代，返回 false 时迭代结束
    /// （类似IEnumerator）
    /// </summary>
    /// <seealso href="https://learn.microsoft.com/zh-cn/dotnet/api/system.collections.ienumerator"/>IEnumerator参考
    public interface IPlayerLoopItem
    {
        bool MoveNext();
    }
}