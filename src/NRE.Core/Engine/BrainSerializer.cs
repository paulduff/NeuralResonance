namespace NRE.Core.Engine;

/// <summary>
/// Serialization for brain state — save/load neural volumes and synapse maps.
/// Extracted from NreEngine for reuse and testability.
/// </summary>
public static class BrainSerializer
{
    public static void WriteVolume(BinaryWriter bw, NeuralVolume vol)
    {
        int w = vol.W, h = vol.H, d = vol.D;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        for (int z = 0; z < d; z++)
        {
            bw.Write(vol.Vm[vol.IndexOf(x, y, z)]);
            bw.Write(vol.Energy[vol.IndexOf(x, y, z)]);
            bw.Write(vol.Theta[vol.IndexOf(x, y, z)]);
            bw.Write(vol.Active[vol.IndexOf(x, y, z)]);
            bw.Write(vol.RegionId[vol.IndexOf(x, y, z)]);
        }
    }

    public static void ReadVolume(BinaryReader br, NeuralVolume vol)
    {
        int w = vol.W, h = vol.H, d = vol.D;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        for (int z = 0; z < d; z++)
        {
            vol.Vm[vol.IndexOf(x, y, z)] = br.ReadSingle();
            vol.Energy[vol.IndexOf(x, y, z)] = br.ReadSingle();
            vol.Theta[vol.IndexOf(x, y, z)] = br.ReadSingle();
            vol.Active[vol.IndexOf(x, y, z)] = br.ReadBoolean();
            vol.RegionId[vol.IndexOf(x, y, z)] = br.ReadByte();
        }
    }

    public static void WriteSynapseMap(BinaryWriter bw, SynapseMap map, int version = 3)
    {
        var pres = map.Pres.ToList();
        bw.Write(pres.Count);
        foreach (int pre in pres)
        {
            bw.Write(pre);
            var outgoing = map.GetOutgoing(pre); if (outgoing == null) continue;
            bw.Write(outgoing.Count);
            for (int i = 0; i < outgoing.Count; i++)
            {
                var s = outgoing[i];
                bw.Write(s.Post);
                bw.Write(s.DelayTicks);
                bw.Write(s.W);
                bw.Write(s.Facil);
                bw.Write(s.Depr);
                bw.Write(s.UsageEma01);
                if (version >= 2)
                {
                    bw.Write(s.PreRelease);
                    bw.Write(s.PostSensitivity);
                }
                if (version >= 3)
                {
                    bw.Write(s.PreTrace);
                    bw.Write(s.PostTrace);
                    bw.Write(s.LastPlasticityStep);
                }
            }
        }
    }

    public static void ReadSynapseMap(BinaryReader br, SynapseMap map, int version = 3)
    {
        map.Clear();
        int preCount = br.ReadInt32();
        for (int p = 0; p < preCount; p++)
        {
            int pre = br.ReadInt32();
            int synCount = br.ReadInt32();
            for (int s = 0; s < synCount; s++)
            {
                int post = br.ReadInt32();
                int delay = br.ReadInt32();
                float w = br.ReadSingle();
                float facil = br.ReadSingle();
                float depr = br.ReadSingle();
                float usage = br.ReadSingle();
                float preRelease = 1.0f;
                float postSensitivity = 1.0f;
                float preTrace = 0.0f;
                float postTrace = 0.0f;
                long lastPlasticityStep = 0;
                if (version >= 2)
                {
                    preRelease = br.ReadSingle();
                    postSensitivity = br.ReadSingle();
                }
                if (version >= 3)
                {
                    preTrace = br.ReadSingle();
                    postTrace = br.ReadSingle();
                    lastPlasticityStep = br.ReadInt64();
                }
                map.Add(pre, post, delay, w);
                var outgoing = map.GetOutgoing(pre); if (outgoing == null) continue;
                var syn = outgoing[outgoing.Count - 1];
                syn.Facil = facil;
                syn.Depr = depr;
                syn.UsageEma01 = usage;
                syn.PreRelease = preRelease;
                syn.PostSensitivity = postSensitivity;
                syn.PreTrace = preTrace;
                syn.PostTrace = postTrace;
                syn.LastPlasticityStep = lastPlasticityStep;
            }
        }
    }
}
