using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HandwritingPad.Services;

public sealed class PlatformInputInjector : IInputInjector
{
    public async Task PasteAsync(CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsWindows())
        {
            SendCtrlVOnWindows();
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            await SendCtrlVOnLinuxAsync(cancellationToken);
            return;
        }

        throw new PlatformNotSupportedException("当前平台未实现自动粘贴。文本已复制到剪贴板。");
    }

    private static async Task SendCtrlVOnLinuxAsync(CancellationToken cancellationToken)
    {
        var isWayland = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")) ||
                        string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase);

        if (!isWayland)
        {
            if (await TryRunAsync("xdotool", new[] { "key", "--clearmodifiers", "ctrl+v" }, cancellationToken))
            {
                return;
            }
        }

        if (await TryRunAsync("ydotool", new[] { "key", "29:1", "47:1", "47:0", "29:0" }, cancellationToken))
        {
            return;
        }

        if (await TryRunAsync("xdotool", new[] { "key", "--clearmodifiers", "ctrl+v" }, cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException("Linux 自动粘贴失败。文本已复制到剪贴板。X11 可配置 xdotool；Wayland 通常需要 ydotoold/uinput 权限。");
    }

    private static async Task<bool> TryRunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static void SendCtrlVOnWindows()
    {
        const ushort vkControl = 0x11;
        const ushort vkV = 0x56;

        var inputs = new[]
        {
            KeyboardInput(vkControl, keyUp: false),
            KeyboardInput(vkV, keyUp: false),
            KeyboardInput(vkV, keyUp: true),
            KeyboardInput(vkControl, keyUp: true)
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException("Windows SendInput 发送 Ctrl+V 失败。文本已复制到剪贴板。");
        }
    }

    private static INPUT KeyboardInput(ushort virtualKey, bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
