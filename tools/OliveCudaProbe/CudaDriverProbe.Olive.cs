// Narrow typed kernel boundary. All five buffers belong to this context and
// have exact component counts plus eight float64 guards at each end.
internal sealed unsafe partial class CudaDriverProbe
{
    internal void LaunchOlive(nint function, ulong state, ulong work, ulong parameters,
        ulong inputs, ulong reports, int count)
    {
        Require();
        if (function == 0 || function != _kernelFunction || count < 1 || count > 32768)
            throw new ArgumentException("Owned olive kernel and bounded population required.");
        ulong[] addresses = [state, work, parameters, inputs, reports];
        int[] components = [14, 14, 5, 9, 5];
        if (addresses.Distinct().Count() != 5) throw new ArgumentException("Aliased kernel buffers.");
        for (int i = 0; i < addresses.Length; ++i)
            if (!_allocations.TryGetValue(addresses[i], out int size) || size != checked(count * components[i] + 16))
                throw new ArgumentException("Wrong kernel buffer size or owner.");
        nint* args = stackalloc nint[6];
        args[0] = (nint)(&state); args[1] = (nint)(&work); args[2] = (nint)(&parameters);
        args[3] = (nint)(&inputs); args[4] = (nint)(&reports); args[5] = (nint)(&count);
        DeviceCheck(Cu.Launch(function, checked((uint)((count + 127) / 128)), 1, 1, 128, 1, 1, 0, 0, (nint)args, 0), "olive cuLaunchKernel");
        DeviceCheck(Cu.Synchronize(), "olive cuCtxSynchronize");
    }
}
