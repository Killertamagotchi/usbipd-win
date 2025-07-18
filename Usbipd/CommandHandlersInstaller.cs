﻿// SPDX-FileCopyrightText: 2024 Frans van Dorsselaer
//
// SPDX-License-Identifier: GPL-3.0-only

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;
using Windows.Win32.System.Services;

namespace Usbipd;

sealed partial class CommandHandlers : ICommandHandlers
{
    Task<ExitCode> ICommandHandlers.Install(IConsole console, CancellationToken cancellationToken)
    {
        ConsoleTools.ReportInfo(console, "install helper");
        var path = Path.GetDirectoryName(Environment.ProcessPath)!;
        ConsoleTools.ReportInfo(console, $"path = {path}");

        {
            ConsoleTools.ReportInfo(console, $"Installing VBoxUSB");
            // See: https://learn.microsoft.com/en-us/windows-hardware/drivers/install/preinstalling-driver-packages
            unsafe // DevSkim: ignore DS172412
            {
                fixed (char* inf = Path.Combine(path, "Drivers", "VBoxUSB.inf"))
                {
                    if (!PInvoke.SetupCopyOEMInf(inf, null, OEM_SOURCE_MEDIA_TYPE.SPOST_PATH, 0, null, 0))
                    {
                        console.ReportError($"SetupCopyOEMInf -> {Marshal.GetLastWin32Error()}");
                        return Task.FromResult(ExitCode.Failure);
                    }
                }
            }
        }

        {
            ConsoleTools.ReportInfo(console, $"Installing VBoxUSBMon");
            // NOTE: This cannot be done from WiX, since WiX cannot create SERVICE_KERNEL_DRIVER; removal from WiX works though.
            using var manager = PInvoke.OpenSCManager(string.Empty, PInvoke.SERVICES_ACTIVE_DATABASE, PInvoke.SC_MANAGER_ALL_ACCESS);
            if (manager.IsInvalid)
            {
                console.ReportError($"OpenSCManager -> {Marshal.GetLastWin32Error()}");
                return Task.FromResult(ExitCode.Failure);
            }
            unsafe // DevSkim: ignore DS172412
            {
                using var service = PInvoke.CreateService(manager, "VBoxUSBMon", "VirtualBox USB Monitor Service",
                    (uint)GENERIC_ACCESS_RIGHTS.GENERIC_ALL, ENUM_SERVICE_TYPE.SERVICE_KERNEL_DRIVER, SERVICE_START_TYPE.SERVICE_DEMAND_START,
                    SERVICE_ERROR.SERVICE_ERROR_NORMAL, Path.Combine(path, "Drivers", "VBoxUSBMon.sys"), null, null, null, null, null);
                if (service.IsInvalid)
                {
                    console.ReportError($"CreateService -> {Marshal.GetLastWin32Error()}");
                    return Task.FromResult(ExitCode.Failure);
                }
            }
        }

        return Task.FromResult(ExitCode.Success);
    }

    Task<ExitCode> ICommandHandlers.Uninstall(IConsole console, CancellationToken cancellationToken)
    {
        ConsoleTools.ReportInfo(console, $"uninstall helper");
        var path = Path.GetDirectoryName(Environment.ProcessPath)!;
        ConsoleTools.ReportInfo(console, $"path = {path}");

        var success = true;

        {
            ConsoleTools.ReportInfo(console, $"Uninstalling VBoxUSB");
            unsafe // DevSkim: ignore DS172412
            {
                BOOL needReboot;
                if (!PInvoke.DiUninstallDriver(HWND.Null, Path.Combine(path, "Drivers", "VBoxUSB.inf"), 0, &needReboot))
                {
                    console.ReportError($"DiUninstallDriver -> {Marshal.GetLastWin32Error()}");
                    success = false;
                    // continue
                }
                // ignore needReboot
            }
        }

        return Task.FromResult(success ? ExitCode.Success : ExitCode.Failure);
    }
}
