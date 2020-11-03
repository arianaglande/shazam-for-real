﻿using System;
using System.Collections.Generic;
using System.Linq;

class LandmarkFinder {
    public const int
        RADIUS_TIME = 47,
        RADIUS_FREQ = 9;

    // Locations per second in a band
    const int RATE = 12;

    static readonly IReadOnlyList<int> BAND_FREQS = new[] { 250, 520, 1450, 3500, 5500 };

    readonly Spectrogram Spectro;
    readonly TimeSpan StripeDuration;
    readonly int MinBin, MaxBin;
    readonly IReadOnlyList<List<(int stripe, int bin)>> Bands;

    public LandmarkFinder(Spectrogram spectro, TimeSpan stripeDuration) {
        Spectro = spectro;
        StripeDuration = stripeDuration;

        MinBin = Math.Max(spectro.FreqToBin(BAND_FREQS.Min()), RADIUS_FREQ);
        MaxBin = Math.Min(spectro.FreqToBin(BAND_FREQS.Max()), spectro.BinCount - RADIUS_FREQ);

        Bands = Enumerable.Range(0, BAND_FREQS.Count - 1)
            .Select(_ => new List<(int, int)>())
            .ToList();
    }

    public void Find(int stripe) {
        for(var bin = MinBin; bin < MaxBin; bin++) {

            if(!IsPeak(stripe, bin, RADIUS_TIME, 0))
                continue;

            if(!IsPeak(stripe, bin, 3, RADIUS_FREQ))
                continue;

            AddLocation(stripe, bin);
        }
    }

    public IEnumerable<IEnumerable<LandmarkInfo>> EnumerateBands() {
        return Bands.Select(locations => locations.Select(LocationToLandmark));
    }

    public IEnumerable<(int stripe, int bin)> EnumerateAllLocations() {
        return Bands.SelectMany(i => i);
    }

    public IEnumerable<LandmarkInfo> EnumerateAllLandmarks() {
        return EnumerateAllLocations().Select(LocationToLandmark);
    }

    int GetBandIndex(int bin) {
        var freq = Spectro.BinToFreq(bin);

        if(freq < BAND_FREQS[0])
            throw new ArgumentOutOfRangeException();

        for(var i = 1; i < BAND_FREQS.Count; i++) {
            if(freq < BAND_FREQS[i])
                return i - 1;
        }

        throw new ArgumentOutOfRangeException();
    }

    LandmarkInfo LocationToLandmark((int, int) loc) {
        var (stripe, bin) = loc;
        return new LandmarkInfo(
            stripe,
            Convert.ToUInt16(64 * bin - 1),
            Convert.ToUInt16(UInt16.MaxValue * Spectro.GetMagnitude(stripe, bin) / Spectro.MaxMagnitude),
            Spectro.BinToFreq(bin)
        );
    }

    bool IsPeak(int stripe, int bin, int stripeRadius, int binRadius) {
        var center = Spectro.GetMagnitude(stripe, bin);
        for(var s = -stripeRadius; s <= stripeRadius; s++) {
            for(var b = -binRadius; b <= binRadius; b++) {
                if(s == 0 && b == 0)
                    continue;
                if(Spectro.GetMagnitude(stripe + s, bin + b) >= center)
                    return false;
            }
        }
        return true;
    }

    void AddLocation(int stripe, int bin) {
        var bandLocations = Bands[GetBandIndex(bin)];

        if(bandLocations.Any()) {
            var capturedDuration = StripeDuration.TotalSeconds * (stripe - bandLocations.First().stripe);
            var allowedCount = 1 + capturedDuration * RATE;
            if(bandLocations.Count > allowedCount) {
                var magnitude = Spectro.GetMagnitude(stripe, bin);
                var pruneIndex = bandLocations.FindLastIndex(l => Spectro.GetMagnitude(l.stripe, l.bin) < magnitude);
                if(pruneIndex < 0)
                    return;

                bandLocations.RemoveAt(pruneIndex);
            }
        }

        bandLocations.Add((stripe, bin));
    }

}
