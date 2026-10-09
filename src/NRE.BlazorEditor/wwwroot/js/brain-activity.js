export const SNAPSHOT_FRESH_MS = 5000;

// The colour scale is a display setting, never an input to the DNNE.
export function firingRateLevel(rateHz, ceilingHz = 200) {
    if (rateHz === null || rateHz === undefined || !Number.isFinite(rateHz) || rateHz < 0) return null;
    if (!Number.isFinite(ceilingHz) || ceilingHz <= 0) throw new RangeError('Rate scale must be positive.');
    return Math.min(1, rateHz / ceilingHz);
}

export function snapshotIsFresh(receivedAt, now) {
    return receivedAt !== null && now >= receivedAt && now - receivedAt <= SNAPSHOT_FRESH_MS;
}

export function observeSnapshot(previous, tick, receivedAt, hasStructures) {
    if (!hasStructures || !Number.isFinite(tick)) return previous;
    return previous.tick === tick ? previous : { tick, receivedAt };
}

export function recentDispatches(traces, now) {
    if (!Array.isArray(traces)) return [];
    return traces.filter(trace => {
        const timestamp = Number(trace.wallClockUnixMs ?? trace.WallClockUnixMs);
        return Number.isFinite(timestamp) && timestamp <= now && now - timestamp <= SNAPSHOT_FRESH_MS;
    });
}
