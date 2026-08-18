using NUnit.Framework;

namespace Tests.EditMode.Player
{
    public class PlayerJoinDataCodecTests
    {
        [Test]
        public void EncodeAndDecode_Succeeds()
        {
            var data = new PlayerJoinData(new ProfileId("valid-profile"));
            Assert.IsTrue(PlayerJoinDataCodec.TryEncode(data, out byte[] token));
            Assert.IsNotNull(token);
            Assert.IsTrue(PlayerJoinDataCodec.TryDecode(token, out PlayerJoinData decoded));
            Assert.AreEqual("valid-profile", decoded.ProfileId.Value);
        }

        [Test]
        public void TryEncode_RejectsInvalidProfile()
        {
            var data = new PlayerJoinData(new ProfileId(""));
            Assert.IsFalse(PlayerJoinDataCodec.TryEncode(data, out byte[] token));
            Assert.IsNull(token);
        }

        [Test]
        public void TryEncode_RejectsOversizedProfile()
        {
            var data = new PlayerJoinData(new ProfileId(new string('a', 65)));
            Assert.IsFalse(PlayerJoinDataCodec.TryEncode(data, out byte[] token));
            Assert.IsNull(token);
        }

        [Test]
        public void TryDecode_RejectsNullToken()
        {
            Assert.IsFalse(PlayerJoinDataCodec.TryDecode(null, out PlayerJoinData data));
            Assert.IsFalse(data.ProfileId.IsValid);
        }

        [Test]
        public void TryDecode_RejectsEmptyToken()
        {
            Assert.IsFalse(PlayerJoinDataCodec.TryDecode(new byte[0], out PlayerJoinData data));
            Assert.IsFalse(data.ProfileId.IsValid);
        }

        [Test]
        public void TryDecode_RejectsIncorrectLength()
        {
            Assert.IsFalse(PlayerJoinDataCodec.TryDecode(new byte[] { 1 }, out PlayerJoinData data));
            Assert.IsFalse(PlayerJoinDataCodec.TryDecode(new byte[] { 3, 1 }, out PlayerJoinData data2));
        }

        [Test]
        public void TryDecode_RejectsUnknownVersion()
        {
            Assert.IsFalse(PlayerJoinDataCodec.TryDecode(new byte[] { 99, 1, 97 }, out PlayerJoinData data));
        }

        [Test]
        public void TryDecode_RejectsProfileLengthMismatch()
        {
            Assert.IsFalse(PlayerJoinDataCodec.TryDecode(new byte[] { 3, 2, 97 }, out PlayerJoinData data));
        }
    }
}
