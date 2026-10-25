using System.Runtime.InteropServices;

namespace NusaLite.Interop;

public static class CoreAudio
{
    public const int EdataFlowRender = 0;
    public const int EroleMultimedia = 1;
    public const uint ClsctxAll = 23;

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    [ClassInterface(ClassInterfaceType.None)]
    private class MmDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.Interface)] out IAudioEndpointVolume volume);

        [PreserveSig]
        int OpenPropertyStore(uint stgmAccess, out IntPtr properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr notify);

        [PreserveSig]
        int GetChannelCount(out uint count);

        [PreserveSig]
        int SetMasterVolumeLevel(float level, ref Guid context);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid context);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float level, ref Guid context);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float level);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);

        [PreserveSig]
        int GetVolumeStepInfo(out uint step, out uint stepCount);

        [PreserveSig]
        int VolumeStepUp(ref Guid context);

        [PreserveSig]
        int VolumeStepDown(ref Guid context);

        [PreserveSig]
        int QueryHardwareSupport(out uint mask);

        [PreserveSig]
        int GetVolumeRange(out float min, out float max, out float increment);
    }

    private sealed class Endpoint : IDisposable
    {
        private IAudioEndpointVolume? _volume;
        private IMMDevice? _device;
        private IMMDeviceEnumerator? _enumerator;

        public Endpoint()
        {
            _enumerator = (IMMDeviceEnumerator)new MmDeviceEnumerator();
            var hr = _enumerator.GetDefaultAudioEndpoint(EdataFlowRender, EroleMultimedia, out _device);
            if (hr != 0 || _device is null)
            {
                throw new COMException("No default render endpoint.", hr);
            }

            var iid = typeof(IAudioEndpointVolume).GUID;
            hr = _device.Activate(ref iid, ClsctxAll, IntPtr.Zero, out _volume);
            if (hr != 0 || _volume is null)
            {
                throw new COMException("Could not activate endpoint volume.", hr);
            }
        }

        public float GetVolume()
        {
            if (_volume is null)
            {
                throw new InvalidOperationException("Endpoint released.");
            }

            return _volume.GetMasterVolumeLevelScalar(out var level) == 0 ? level : throw new COMException("GetVolume failed.");
        }

        public bool GetMute()
        {
            if (_volume is null)
            {
                throw new InvalidOperationException("Endpoint released.");
            }

            return _volume.GetMute(out var mute) == 0 ? mute : throw new COMException("GetMute failed.");
        }

        public void SetVolume(float level)
        {
            if (_volume is null)
            {
                throw new InvalidOperationException("Endpoint released.");
            }

            var context = Guid.Empty;
            _volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref context);
        }

        public void SetMute(bool mute)
        {
            if (_volume is null)
            {
                throw new InvalidOperationException("Endpoint released.");
            }

            var context = Guid.Empty;
            _volume.SetMute(mute, ref context);
        }

        public void Dispose()
        {
            if (_volume is not null)
            {
                Marshal.FinalReleaseComObject(_volume);
                _volume = null;
            }

            if (_device is not null)
            {
                Marshal.FinalReleaseComObject(_device);
                _device = null;
            }

            if (_enumerator is not null)
            {
                Marshal.FinalReleaseComObject(_enumerator);
                _enumerator = null;
            }
        }
    }

    public sealed class AudioService : IDisposable
    {
        private Endpoint? _endpoint;

        public bool IsAvailable => EnsureEndpoint() is not null;

        private Endpoint? EnsureEndpoint()
        {
            if (_endpoint is not null)
            {
                return _endpoint;
            }

            try
            {
                _endpoint = new Endpoint();
                return _endpoint;
            }
            catch (Exception ex)
            {
                Infrastructure.Log.Write("Audio endpoint unavailable.", ex);
                _endpoint = null;
                return null;
            }
        }

        public int GetVolumePercent()
        {
            var endpoint = EnsureEndpoint();
            if (endpoint is null)
            {
                return 50;
            }

            try
            {
                return (int)Math.Round(endpoint.GetVolume() * 100);
            }
            catch
            {
                DisposeEndpoint();
                return 50;
            }
        }

        public bool GetMuted()
        {
            var endpoint = EnsureEndpoint();
            if (endpoint is null)
            {
                return false;
            }

            try
            {
                return endpoint.GetMute();
            }
            catch
            {
                DisposeEndpoint();
                return false;
            }
        }

        public void SetVolumePercent(int percent)
        {
            var endpoint = EnsureEndpoint();
            if (endpoint is null)
            {
                return;
            }

            try
            {
                endpoint.SetVolume(Math.Clamp(percent, 0, 100) / 100f);
            }
            catch
            {
                DisposeEndpoint();
            }
        }

        public void SetMuted(bool muted)
        {
            var endpoint = EnsureEndpoint();
            if (endpoint is null)
            {
                return;
            }

            try
            {
                endpoint.SetMute(muted);
            }
            catch
            {
                DisposeEndpoint();
            }
        }

        private void DisposeEndpoint()
        {
            _endpoint?.Dispose();
            _endpoint = null;
        }

        public void Dispose() => DisposeEndpoint();
    }
}
