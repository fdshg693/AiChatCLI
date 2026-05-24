using System.Threading;

namespace AiChatCLI;

public static class SubAgentExecutionContext
{
    private static readonly AsyncLocal<int> Depth = new();

    public static bool IsActive => Depth.Value > 0;

    public static IDisposable Enter()
    {
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            Depth.Value = Math.Max(0, Depth.Value - 1);
            _disposed = true;
        }
    }
}
