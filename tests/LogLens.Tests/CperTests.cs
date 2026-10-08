using System.Buffers.Binary;
using LogLens.Core;
using Xunit;

namespace LogLens.Tests;

public sealed class CperTests
{
    public static byte[] Record(byte error = 1, uint severity = 1, ulong valid = 0x124)
    {
        var data = new byte[392]; "CPER"u8.CopyTo(data); data[4] = 0x10; data[5] = 2;
        Write32(data, 6, uint.MaxValue); data[10] = 1; Write32(data, 12, severity); Write32(data, 20, (uint)data.Length);
        Write32(data, 128, 200); Write32(data, 132, 192); data[137] = 3;
        new Guid("9876ccad-47b4-4bdb-b65e-16f193c4f3db").TryWriteBytes(data.AsSpan(144, 16));
        Write32(data, 176, severity); BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(200), valid);
        data[210] = error; data[213] = 1; BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(352), 5);
        return data;
    }
    private static void Write32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
    [Theory]
    [InlineData(1, "cache")]
    [InlineData(2, "translation lookaside buffer (TLB)")]
    [InlineData(4, "processor bus")]
    [InlineData(8, "processor microarchitecture")]
    public void ValidatedProcessorErrorIsSpecific(byte value, string kind)
    {
        var decoded = CperDecoder.Decode(Convert.ToHexString(Record(value)));
        Assert.NotNull(decoded); Assert.Equal("Fatal", decoded.Severity); Assert.NotNull(decoded.Processor);
        Assert.Equal(kind, decoded.Processor.Kind); Assert.Equal((ulong)5, decoded.Processor.ProcessorId); Assert.Equal((byte)1, decoded.Processor.CacheLevel);
    }
    [Theory]
    [InlineData(0, "Recoverable")]
    [InlineData(1, "Fatal")]
    [InlineData(2, "Corrected")]
    [InlineData(3, "Informational")]
    public void SeverityComesFromCperNotEventLevel(uint value, string expected) => Assert.Equal(expected, CperDecoder.Decode(Convert.ToHexString(Record(severity: value)))?.Severity);
    [Fact] public void UnknownErrorKindCannotBeCalledCache() => Assert.Null(CperDecoder.Decode(Convert.ToHexString(Record(error: 3)))?.Processor);
    [Fact] public void AbsentValidityBitCannotBecomeProcessorFinding() => Assert.Null(CperDecoder.Decode(Convert.ToHexString(Record(valid: 0)))?.Processor);
    [Fact] public void UnvalidatedProcessorIdentityAndLevelAreOmitted()
    {
        var result = CperDecoder.Decode(Convert.ToHexString(Record(valid: 4)))!;
        Assert.NotNull(result.Processor); Assert.Null(result.Processor.ProcessorId); Assert.Null(result.Processor.CacheLevel);
    }
    [Fact] public void EveryTruncatedPrefixIsRejectedWithoutThrowing()
    {
        var data = Record();
        for (var size = 0; size < data.Length; size++) Assert.Null(CperDecoder.Decode(Convert.ToHexString(data.AsSpan(0, size))));
    }
    [Fact] public void InvalidSignatureRevisionAndLengthAreRejected()
    {
        foreach (var offset in new[] { 0, 5, 6, 10, 20, 137 })
        {
            var data = Record(); data[offset] ^= 0xFF; Assert.Null(CperDecoder.Decode(Convert.ToHexString(data)));
        }
    }
    [Fact] public void OverflowingSectionOffsetIsRejected()
    {
        var data = Record(); Write32(data, 128, uint.MaxValue); Assert.Null(CperDecoder.Decode(Convert.ToHexString(data)));
    }
    [Theory]
    [InlineData("not-hex")]
    [InlineData("")]
    public void InvalidHexIsRejected(string value) => Assert.Null(CperDecoder.Decode(value));
    [Fact] public void OversizedPayloadIsRejected() => Assert.Null(CperDecoder.Decode(new string('0', CperDecoder.MaximumHexLength + 2)));
    [Fact] public void FatalCacheFindingDoesNotDiagnoseDefectiveCpu()
    {
        var e = AccuracyFixtures.E("Microsoft-Windows-WHEA-Logger", 1, fields: [("RawData", Convert.ToHexString(Record()))]);
        var result = Assert.Single(new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period));
        Assert.Equal("Fatal processor cache error", result.Title); Assert.Equal(Severity.Critical, result.Severity);
        Assert.Contains(result.Findings, f => f.Code == "processor-error" && f.Classification == EvidenceClass.ConfirmedObservation && f.Text.Contains("identifier: 5", StringComparison.Ordinal));
        Assert.Contains(result.Findings, f => f.Code == "processor-cause" && f.Classification == EvidenceClass.PossibleCause);
        Assert.Contains(result.Recommendations, r => r.Code == "cpu-stock");
        Assert.DoesNotContain(result.Recommendations, r => r.Code == "memory");
        Assert.DoesNotContain("CPU is defective", result.Explanation);
    }
    [Fact] public void CorrectedCacheErrorIsNotReportedAsFatal()
    {
        var e = AccuracyFixtures.E("Microsoft-Windows-WHEA-Logger", 19, fields: [("RawData", Convert.ToHexString(Record(severity: 2)))]) with { Level = 2 };
        var result = Assert.Single(new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period));
        Assert.Equal("Corrected processor cache error", result.Title); Assert.Equal(Severity.Warning, result.Severity);
        Assert.DoesNotContain("fatal", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void ApplicationExceptionMeaningIsNotHardwareDiagnosis()
    {
        var e = AccuracyFixtures.E("Application Error", 1000, channel: "Application", fields: [("AppName", "game.exe"), ("ExceptionCode", "c0000005")]);
        var incident = Assert.Single(new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period));
        Assert.Contains(incident.Findings, f => f.Code == "exception-code" && f.Text.Contains("access violation", StringComparison.Ordinal));
        Assert.DoesNotContain("RAM is defective", incident.Explanation);
    }
    [Fact] public void BugCheckStructuredParameterIsDecodedWithoutParsingLocalizedMessage()
    {
        var e = AccuracyFixtures.E("BugCheck", 1001, fields: [("param1", "0x00000124 (0x0, 0x1, 0x2, 0x3)")]);
        var incident = Assert.Single(new DiagnosticEngine().Analyze([e], AccuracyFixtures.Period));
        Assert.Contains(incident.Findings, f => f.Code == "stop-code" && f.Text.Contains("WHEA_UNCORRECTABLE_ERROR", StringComparison.Ordinal));
    }
}
