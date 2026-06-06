using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Gomoku_Avalonia.Services;

public interface IAppExitService
{
    void Exit();
}

public sealed class AppExitService : IAppExitService
{
    public void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
            return;
        }

        Environment.Exit(0);
    }
}
