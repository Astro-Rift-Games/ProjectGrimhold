using System;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class RaidAdmissionPayloadChunkingTests
{
    private static byte[] CreatePayload(int length)
    {
        var payload = new byte[length];
        for (int index = 0; index < length; index++)
        {
            payload[index] = (byte)(index % 251);
        }

        return payload;
    }

    [Test]
    public void TrySplit_EveryChunkFitsUnderFusionRpcLimit()
    {
        byte[] payload = CreatePayload(844);

        Assert.That(RaidAdmissionPayloadChunking.TrySplit(payload, out byte[][] chunks), Is.True);
        Assert.That(chunks.Length, Is.GreaterThan(1));
        Assert.That(chunks.All(chunk => chunk.Length <= RaidAdmissionPayloadChunking.MaxChunkBytes), Is.True);
        Assert.That(RaidAdmissionPayloadChunking.MaxChunkBytes, Is.LessThan(512));
    }

    [Test]
    public void TrySplit_RejectsEmptyAndOversizedPayloads()
    {
        Assert.That(RaidAdmissionPayloadChunking.TrySplit(null, out _), Is.False);
        Assert.That(RaidAdmissionPayloadChunking.TrySplit(Array.Empty<byte>(), out _), Is.False);
        Assert.That(RaidAdmissionPayloadChunking.TrySplit(
            CreatePayload(RaidAdmissionPayloadChunking.MaxPayloadBytes + 1), out _), Is.False);
    }

    [Test]
    public void Assembler_ReassemblesOutOfOrderChunksExactly()
    {
        byte[] payload = CreatePayload(844);
        RaidAdmissionPayloadChunking.TrySplit(payload, out byte[][] chunks);
        var assembler = new RaidAdmissionPayloadAssembler();

        byte[] result = null;
        RaidAdmissionPayloadAssemblyResult last = RaidAdmissionPayloadAssemblyResult.Incomplete;
        for (int index = chunks.Length - 1; index >= 0; index--)
        {
            last = assembler.Accept(index, chunks.Length, chunks[index], out result);
        }

        Assert.That(last, Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Completed));
        Assert.That(result, Is.EqualTo(payload));
    }

    [Test]
    public void Assembler_SingleChunkCompletesImmediately()
    {
        byte[] payload = CreatePayload(100);
        RaidAdmissionPayloadChunking.TrySplit(payload, out byte[][] chunks);
        var assembler = new RaidAdmissionPayloadAssembler();

        Assert.That(assembler.Accept(0, chunks.Length, chunks[0], out byte[] result),
            Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Completed));
        Assert.That(result, Is.EqualTo(payload));
    }

    [Test]
    public void Assembler_RejectsMalformedChunks()
    {
        var assembler = new RaidAdmissionPayloadAssembler();
        byte[] chunk = CreatePayload(10);

        Assert.That(assembler.Accept(0, 0, chunk, out _), Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Rejected));
        Assert.That(new RaidAdmissionPayloadAssembler().Accept(2, 2, chunk, out _), Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Rejected));
        Assert.That(new RaidAdmissionPayloadAssembler().Accept(0, 2, null, out _), Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Rejected));
        Assert.That(new RaidAdmissionPayloadAssembler().Accept(
            0, 2, CreatePayload(RaidAdmissionPayloadChunking.MaxChunkBytes + 1), out _),
            Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Rejected));
        Assert.That(new RaidAdmissionPayloadAssembler().Accept(
            0, RaidAdmissionPayloadChunking.MaxChunkCount + 1, chunk, out _),
            Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Rejected));
    }

    [Test]
    public void Assembler_RejectsInconsistentChunkCountAndDuplicates()
    {
        byte[] chunk = CreatePayload(10);

        var mismatched = new RaidAdmissionPayloadAssembler();
        Assert.That(mismatched.Accept(0, 3, chunk, out _), Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Incomplete));
        Assert.That(mismatched.Accept(1, 4, chunk, out _), Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Rejected));

        var duplicated = new RaidAdmissionPayloadAssembler();
        Assert.That(duplicated.Accept(1, 3, chunk, out _), Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Incomplete));
        Assert.That(duplicated.Accept(1, 3, chunk, out _), Is.EqualTo(RaidAdmissionPayloadAssemblyResult.Rejected));
    }
}
