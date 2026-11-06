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

    /// <summary>Same legal 30 + 4 weapon snapshot measured by the backend admission test.</summary>
    internal static byte[] CreateMaximumCapacityTicket()
    {
        var modifiers = new { primary = new { attribute = "strength", grade = "E" },
            gemSocketCount = 2, gems = new[] { "RES_RUBY_GEM", "RES_SAPPHIRE_GEM" },
            secondary = new { attribute = "intelligence", grade = "S" } };
        var unit = new { lootId = "arming_sword", modifiers };
        var equipment = new System.Collections.Generic.Dictionary<string, object>
        {
            ["weaponSlot1"] = unit, ["weaponSlot2"] = unit, ["offHand1"] = unit, ["offHand2"] = unit,
            ["helmet"] = null, ["armor"] = null, ["gloves"] = null, ["boots"] = null
        };
        var payload = new { characterId = "0123456789abcdef01234567", characterName = "Instances",
            raidCode = "123456", reservationId = "0123456789abcdef0123456789abcdef",
            snapshot = new { level = 1, experience = 0,
                characterAttributes = new { vitality = 5, resistance = 5, strength = 5, dexterity = 5,
                    intelligence = 5, luck = 5, availablePoints = 0 },
                preparedAbilities = new { slot1 = "", slot2 = "" },
                items = Enumerable.Range(0, 30).Select(_ => new { lootId = "arming_sword", amount = 1, modifiers }).ToArray(),
                preparedEquipment = equipment, lastAppliedProgressionResultSequence = 0 },
            issuedAt = 1700000000L, expiresAt = 4102444800L };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        string body = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        // Synthetic signature: chunking preserves opaque signed bytes, it does not authenticate.
        return System.Text.Encoding.UTF8.GetBytes(body + "." + new string('0', 64));
    }

    [Test]
    public void MaximumCapacityTicket_ReassemblesExactlyWithinSixteenKiBBound()
    {
        byte[] payload = CreateMaximumCapacityTicket();
        Assert.That(payload.Length, Is.EqualTo(10423), "Keep parity with the measured backend fixture.");
        Assert.That(RaidAdmissionPayloadChunking.TrySplit(payload, out byte[][] chunks), Is.True);
        Assert.That(chunks.Length, Is.EqualTo(28));
        var assembler = new RaidAdmissionPayloadAssembler();
        byte[] result = null;
        for (int index = chunks.Length - 1; index >= 0; index--)
            Assert.That(assembler.Accept(index, chunks.Length, chunks[index], out result),
                Is.EqualTo(index == 0 ? RaidAdmissionPayloadAssemblyResult.Completed : RaidAdmissionPayloadAssemblyResult.Incomplete));
        Assert.That(result, Is.EqualTo(payload));
    }

    [Test]
    public void SixteenKiBBound_AcceptsBoundaryAndRejectsOneByteBeyondIt()
    {
        Assert.That(RaidAdmissionPayloadChunking.MaxPayloadBytes, Is.EqualTo(16384));
        Assert.That(RaidAdmissionPayloadChunking.MaxChunkBytes, Is.EqualTo(384));
        Assert.That(RaidAdmissionPayloadChunking.MaxChunkCount, Is.EqualTo(43));
        byte[] boundary = CreatePayload(16384);
        Assert.That(RaidAdmissionPayloadChunking.TrySplit(boundary, out byte[][] chunks), Is.True);
        var assembler = new RaidAdmissionPayloadAssembler();
        byte[] result = null;
        for (int index = 0; index < chunks.Length; index++)
            assembler.Accept(index, chunks.Length, chunks[index], out result);
        Assert.That(result, Is.EqualTo(boundary));
        Assert.That(RaidAdmissionPayloadChunking.TrySplit(CreatePayload(16385), out var rejected), Is.False);
        Assert.That(rejected, Is.Null);
        // A malicious sender can claim a legal count but exceed the byte bound by one.
        assembler = new RaidAdmissionPayloadAssembler();
        for (int index = 0; index < 43; index++)
            Assert.That(assembler.Accept(index, 43, CreatePayload(index == 42 ? 257 : 384), out result),
                Is.EqualTo(index == 42 ? RaidAdmissionPayloadAssemblyResult.Rejected : RaidAdmissionPayloadAssemblyResult.Incomplete));
        Assert.That(result, Is.Null);
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
