using System.Buffers.Binary;

namespace LogLens.Core;

public sealed record ProcessorError(string Kind, byte? CacheLevel, ulong? ProcessorId, string Severity);
public sealed record CperRecord(string Severity, IReadOnlyList<string> Sections, ProcessorError? Processor);

// Narrow CPER decoder: header, section descriptors and validated generic-processor fields only.
// Layout/constants: Microsoft win32metadata cper.h/cperguid.h and WHEA ntddk documentation.
// Never decode machine addresses, serials, arbitrary FRU strings or vendor MCA bitfields here.
public static class CperDecoder
{
    public const int MaximumHexLength = 65536;
    public static CperRecord? Decode(string hex)
    {
        if (hex.Length < 256 || hex.Length > MaximumHexLength || hex.Length % 2 != 0) return null;
        byte[] bytes;
        try { bytes = Convert.FromHexString(hex); } catch (FormatException) { return null; }
        var data = bytes.AsSpan();
        if (!data[..4].SequenceEqual("CPER"u8) || data[5] != 2 || U32(data, 6) != uint.MaxValue) return null;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data[10..]);
        var length = U32(data, 20); var severity = SeverityName(U32(data, 12));
        if (count is 0 or > 64 || length != data.Length || 128 + count * 72 > data.Length || severity == null) return null;
        var sections = new List<string>(); var ranges = new List<(uint Start, uint End)>(); ProcessorError? processor = null;
        for (var index = 0; index < count; index++)
        {
            var descriptor = 128 + index * 72;
            var offset = U32(data, descriptor); var size = U32(data, descriptor + 4);
            if (offset < 128 + count * 72 || size == 0 || (ulong)offset + size > length || data[descriptor + 9] != 3) return null;
            var end = offset + size;
            if (ranges.Any(r => offset < r.End && end > r.Start)) return null;
            ranges.Add((offset, end));
            var sectionSeverity = SeverityName(U32(data, descriptor + 48));
            if (sectionSeverity == null) return null;
            var type = new Guid(data.Slice(descriptor + 16, 16)).ToString();
            var name = type switch
            {
                "9876ccad-47b4-4bdb-b65e-16f193c4f3db" => "generic processor",
                "dc3ea0b0-a144-4797-b95b-53fa242b6e1d" => "x86/x64 processor",
                "8a1e1d01-42f9-4557-9c33-565e5cc3f7e8" => "processor machine-check registers",
                "a5bc1114-6f64-4ede-b863-3e83ed7c83b1" => "platform memory",
                "d995e954-bbc1-430f-ad91-b44dcb3c6f35" => "PCI Express",
                "81212a96-09ed-4996-9471-8d729c8e69ed" => "firmware record reference",
                _ => "other platform-specific data"
            };
            sections.Add($"{name} ({sectionSeverity.ToLowerInvariant()})");
            if (type != "9876ccad-47b4-4bdb-b65e-16f193c4f3db" || size < 192) continue;
            var section = data.Slice((int)offset, (int)size);
            var valid = BinaryPrimitives.ReadUInt64LittleEndian(section);
            if ((valid & 4) == 0) continue;
            var kind = section[10] switch { 1 => "cache", 2 => "translation lookaside buffer (TLB)", 4 => "processor bus", 8 => "processor microarchitecture", _ => null };
            if (kind == null || processor != null) continue;
            byte? cacheLevel = (valid & 32) != 0 ? section[13] : null;
            ulong? processorId = (valid & 256) != 0 ? BinaryPrimitives.ReadUInt64LittleEndian(section[152..]) : null;
            processor = new(kind, cacheLevel, processorId, sectionSeverity);
        }
        return new(severity, sections.Distinct().ToArray(), processor);
    }
    private static uint U32(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    private static string? SeverityName(uint value) => value switch { 0 => "Recoverable", 1 => "Fatal", 2 => "Corrected", 3 => "Informational", _ => null };
}
